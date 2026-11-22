using System.Collections.Concurrent;
using System.Net.Quic;

namespace Http3Core.Protocol;

public sealed class Http3Connection
{
    public Http3Settings? PeerSettings { get; private set; }
    public QuicStream? QpackEncoderStream { get; private set; }
    public Task Initialized => initialized.Task;

    public Http3Connection(QuicConnection connection) => this.connection = connection;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Open the server's own HTTP/3 control stream.
        await using var controlStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, cancellationToken);

        // Identify it as an HTTP/3 control stream.
        await QuicVariableInteger.WriteAsync(controlStream, Http3StreamType.Control, cancellationToken);

        // Send the initial SETTINGS frame.
        var settings = new Http3Settings();

        settings.Set(Http3Settings.QpackMaxTableCapacity, 4096);
        settings.Set(Http3Settings.MaxFieldSectionSize, 65536);
        settings.Set(Http3Settings.QpackBlockedStreams, 10);
        settings.Set(0x21, 0);
        await Http3FrameWriter.WriteSettingsAsync(controlStream, settings, cancellationToken);

        Console.WriteLine("Server control stream opened.");
        Console.WriteLine("Server SETTINGS sent.");

        await OpenQpackStreamsAsync(cancellationToken);
        initialized.TrySetResult(true);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                QuicStream stream;

                try
                {
                    stream = await connection.AcceptInboundStreamAsync(cancellationToken);
                }
                catch (QuicException ex)
                {
                    Console.WriteLine($"QUIC connection closed: {ex.QuicError}");
                    break;
                }

                var task = HandleStreamAsync(stream, cancellationToken);
                activeStreams[stream.Id] = task;
                _ = RemoveCompletedStreamAsync(stream.Id, task);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("HTTP/3 connection shutting down.");
        }
        finally
        {
            Console.WriteLine($"Waiting for {activeStreams.Count} active stream handlers...");
            await Task.WhenAll([.. activeStreams.Values]);
            Console.WriteLine("All stream handlers finished.");
        }
    }

    public void RegisterOutstandingFieldSection(long streamId, bool referencesDynamicTable)
    {
        if (referencesDynamicTable)
            outstandingStreams.Add(streamId);
    }    

    async Task HandleStreamAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        await using (stream)
        {
            try
            {
                if (stream.Type == QuicStreamType.Bidirectional)
                {
                    Console.WriteLine($"Received bidirectional stream {stream.Id}");
                    await HandleRequestStreamAsync(stream, cancellationToken);
                    return;                
                }

                var streamType = await QuicVariableInteger.ReadAsync(stream, cancellationToken);

                Console.WriteLine($"Received unidirectional stream {stream.Id}, type: 0x{streamType:X}");

                switch (streamType)
                {
                    case Http3StreamType.Control:
                        await HandleControlStreamAsync(stream, cancellationToken);
                        break;

                    case Http3StreamType.Push:
                        Console.WriteLine("Push stream received.");
                        break;

                    case Http3StreamType.QpackEncoder:
                        await HandleQpackStreamAsync(stream, "encoder", cancellationToken);
                        break;

                    case Http3StreamType.QpackDecoder:
                        await HandleQpackStreamAsync(stream, "decoder", cancellationToken);
                        break;

                    default:
                        Console.WriteLine(
                            $"Unknown stream type: 0x{streamType:X}");
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal connection shutdown.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Stream {stream.Id} failed: {ex.Message}");
            }
        }
    }

    async Task HandleControlStreamAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref controlStreamReceived, 1) != 0)
            throw new InvalidDataException("A second HTTP/3 control stream was received.");

        Console.WriteLine("Control stream received.");

        // The first frame on a control stream MUST be SETTINGS.
        var frame = await Http3FrameReader.ReadAsync(stream, cancellationToken);
        if (frame.Type != Http3FrameWriter.SettingsFrameType)
            throw new InvalidDataException("The first control stream frame must be SETTINGS.");

        Console.WriteLine("Initial SETTINGS frame received.");

        // Decode the SETTINGS payload.
        PeerSettings = await Http3Settings.DecodePayloadAsync(frame.Payload, cancellationToken);
        foreach (var setting in PeerSettings.Values)
            Console.WriteLine($"Setting 0x{setting.Key:X} = {setting.Value}");

        // Further control frames will be handled here later.
        // Keep the stream open and wait for more frames.
        while (true)
        {
            frame = await Http3FrameReader.ReadAsync(stream, cancellationToken);
            Console.WriteLine($"Control frame received: 0x{frame.Type:X}");
        }
    }

    async Task RemoveCompletedStreamAsync(long streamId, Task task)
    {
        try
        {
            await task;
        }
        finally
        {
            activeStreams.TryRemove(streamId, out _);
        }
    }

    async Task OpenQpackStreamsAsync(CancellationToken cancellationToken)
    {
        // QPACK encoder stream
        QpackEncoderStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, cancellationToken);
        await QuicVariableInteger.WriteAsync(QpackEncoderStream, Http3StreamType.QpackEncoder, cancellationToken);

        // QPACK decoder stream
        qpackDecoderStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, cancellationToken);

        await QuicVariableInteger.WriteAsync(qpackDecoderStream, Http3StreamType.QpackDecoder, cancellationToken);

        Console.WriteLine("QPACK encoder and decoder streams opened.");
    }

    async Task HandleQpackStreamAsync(QuicStream stream, string name, CancellationToken cancellationToken)
    {
        Console.WriteLine($"QPACK {name} stream received: {stream.Id}");

        if (name == "encoder")
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await QpackEncoderStreamInstruction.ReadAsync(stream, peerDynamicTable, cancellationToken);
                insertCountTracker.NotifyInsertCountChanged();
                Console.WriteLine($"QPACK Insert Count: {peerDynamicTable.InsertCount}");

                int previousInsertCount = peerDynamicTable.InsertCount;
                await QpackEncoderStreamInstruction.ReadAsync(stream, peerDynamicTable, cancellationToken);
                int newInsertCount = peerDynamicTable.InsertCount;
                insertCountTracker.NotifyInsertCountChanged();
                if (newInsertCount > previousInsertCount)
                {
                    ulong increment = (ulong)(newInsertCount - previousInsertCount);
                    byte[] instruction = QpackDecoderStreamInstruction.EncodeInsertCountIncrement(increment);
                    await WriteQpackDecoderInstructionAsync(instruction, cancellationToken);
                }
                Console.WriteLine($"QPACK Insert Count: {newInsertCount}");
            }
        }
        else
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var instruction = await QpackDecoderStreamInstruction.ReadAsync(stream, cancellationToken);
                Console.WriteLine($"QPACK decoder instruction: {instruction.Type}: {instruction.Value}");
                switch (instruction.Type)
                {
                    case QpackDecoderStreamInstructionType.SectionAcknowledgment:
                        outstandingStreams.Acknowledge((long)instruction.Value);
                        Console.WriteLine($"Section Acknowledgment received for stream {instruction.Value}");
                        break;
                    case QpackDecoderStreamInstructionType.StreamCancellation:
                        outstandingStreams.Cancel((long)instruction.Value);
                        Console.WriteLine($"Stream Cancellation received for stream {instruction.Value}");
                        break;
                    case QpackDecoderStreamInstructionType.InsertCountIncrement:
                        // Already handled by the existing insert-count logic.
                        break;
                }                
            }
        }
    }

    async Task WriteQpackDecoderInstructionAsync(byte[] instruction, CancellationToken cancellationToken)
    {
        await qpackDecoderWriteLock.WaitAsync(cancellationToken);
        try
        {
            await qpackDecoderStream!.WriteAsync(instruction, cancellationToken);
        }
        finally
        {
            qpackDecoderWriteLock.Release();
        }
    }

    async Task HandleRequestStreamAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        // Read the first HTTP/3 frame.
        var frame = await Http3FrameReader.ReadAsync(stream, cancellationToken);
        if (frame.Type != 0x01) // HEADERS
            throw new InvalidDataException($"Expected HEADERS frame, received 0x{frame.Type:X}.");

        Console.WriteLine($"Received HEADERS frame on stream {stream.Id}");

        bool referencesDynamicTable = false;

        using var payload = new MemoryStream(frame.Payload);
        try
        {
            var fields = await QpackFieldSectionDecoder.DecodeAsync(payload, peerDynamicTable, insertCountTracker,
                (ulong)peerDynamicTable.InsertCount, 4096,
                () =>
                {
                    if (!referencesDynamicTable)
                    {
                        referencesDynamicTable = true;
                        outstandingStreams.Add(stream.Id);
                    }
                }, cancellationToken);
            foreach (var field in fields)
                Console.WriteLine($"{field.Name}: {field.Value}");
        }
        catch
        {
            if (referencesDynamicTable)
                await SendQpackStreamCancellationAsync(stream.Id, cancellationToken);
            throw;
        }

        // Send Section Acknowledgment if dynamic table entries were referenced.
        if (referencesDynamicTable)
        {
            byte[] instruction = QpackDecoderStreamInstruction.EncodeSectionAcknowledgment(stream.Id);
            await WriteQpackDecoderInstructionAsync(instruction, cancellationToken);

            Console.WriteLine($"Section Acknowledgment sent for stream {stream.Id}");
        }
    }    
    
    async Task SendQpackStreamCancellationAsync(long streamId, CancellationToken cancellationToken)
    {
        var instruction = QpackDecoderStreamInstruction.EncodeStreamCancellation(streamId);
        await WriteQpackDecoderInstructionAsync(instruction, cancellationToken);
        Console.WriteLine($"Stream Cancellation sent for stream {streamId}");
    }
    
    readonly QuicConnection connection;
    readonly ConcurrentDictionary<long, Task> activeStreams = [];
    readonly QpackDynamicTable peerDynamicTable = new(4096);
    readonly QpackInsertCountTracker insertCountTracker = new();
    readonly SemaphoreSlim qpackDecoderWriteLock = new(1, 1);
    readonly TaskCompletionSource<bool> initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly QpackOutstandingStreamManager outstandingStreams = new();

    QuicStream? qpackDecoderStream;
    int controlStreamReceived;
}
