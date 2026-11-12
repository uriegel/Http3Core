namespace Http3Core.Protocol;

public static class Http3FrameWriter
{
    public const ulong SettingsFrameType = 0x04;

    public static async ValueTask WriteAsync(Stream stream, ulong frameType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        await QuicVariableInteger.WriteAsync(stream, frameType, cancellationToken);
        await QuicVariableInteger.WriteAsync(stream, (ulong)payload.Length, cancellationToken);
        if (!payload.IsEmpty)
            await stream.WriteAsync(payload, cancellationToken);
    }

    public static ValueTask WriteSettingsAsync(Stream stream, CancellationToken cancellationToken = default)
        => WriteAsync(stream, SettingsFrameType, ReadOnlyMemory<byte>.Empty, cancellationToken);

    public static async ValueTask WriteSettingsAsync(Stream stream, Http3Settings settings, CancellationToken cancellationToken = default)
    {
        var payload = await settings.EncodePayloadAsync(cancellationToken);
        await WriteAsync(stream, SettingsFrameType, payload, cancellationToken);
    }    
}