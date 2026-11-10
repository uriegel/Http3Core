using System.Net.Quic;

namespace Http3Core.Protocol;

public sealed class Http3Connection
{
    public Http3Connection(QuicConnection connection) => this.connection = connection;

    public async Task RunAsync(CancellationToken cancellationToken = default)
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

            _ = HandleStreamAsync(stream, cancellationToken);
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

        // Further control frames will be handled here later.
        // Keep the stream open and wait for more frames.
        while (true)
        {
            frame = await Http3FrameReader.ReadAsync(stream, cancellationToken);
            Console.WriteLine($"Control frame received: 0x{frame.Type:X}");
        }
    }

    readonly QuicConnection connection;
    int controlStreamReceived;
}