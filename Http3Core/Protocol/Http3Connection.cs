using System.Collections.Concurrent;
using System.Net.Quic;

namespace Http3Core.Protocol;

public sealed class Http3Connection
{
    public Http3Settings? PeerSettings { get; private set; }
    
    public Http3Connection(QuicConnection connection) => this.connection = connection;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Open the server's own HTTP/3 control stream.
        await using var controlStream = await connection.OpenOutboundStreamAsync( QuicStreamType.Unidirectional, cancellationToken);

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

    async Task HandleStreamAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        await using (stream)
        {
            try
            {
                if (stream.Type == QuicStreamType.Bidirectional)
                {
                    Console.WriteLine($"Received bidirectional stream {stream.Id}");

                    // Request handling will come later.
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
                        Console.WriteLine("QPACK encoder stream received.");
                        break;

                    case Http3StreamType.QpackDecoder:
                        Console.WriteLine("QPACK decoder stream received.");
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

    readonly QuicConnection connection;
    readonly ConcurrentDictionary<long, Task> activeStreams = [];
    int controlStreamReceived;
}