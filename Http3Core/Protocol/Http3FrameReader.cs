namespace Http3Core.Protocol;

public static class Http3FrameReader
{
    public static async ValueTask<Http3Frame> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var type = await QuicVariableInteger.ReadAsync(stream, cancellationToken);
        var length = await QuicVariableInteger.ReadAsync(stream, cancellationToken);
        if (length > int.MaxValue)
            throw new InvalidDataException("HTTP/3 frame is too large.");

        var payload = new byte[(int)length];
        if (payload.Length > 0)
            await stream.ReadExactlyAsync(payload, cancellationToken);
        return new Http3Frame(type, payload);
    }
}