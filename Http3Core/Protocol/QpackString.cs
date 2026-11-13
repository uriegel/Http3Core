namespace Http3Core.Protocol;

public static class QpackString
{
    public static byte[] Encode(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);

        using var stream = new MemoryStream();

        // H = 0: no Huffman encoding.
        // The remaining 7 bits contain the string length.
        var length = QpackInteger.Encode((ulong)bytes.Length, 7);
        stream.Write(length);
        stream.Write(bytes);
        return stream.ToArray();
    }

    public static async ValueTask WriteAsync(Stream stream, string value, CancellationToken cancellationToken = default)
    {
        var bytes = Encode(value);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    public static async ValueTask<string> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // The high bit is the Huffman flag.
        var first = new byte[1];
        await stream.ReadExactlyAsync(first, cancellationToken);

        bool huffman = (first[0] & 0x80) != 0;
        if (huffman)
            throw new NotSupportedException("QPACK Huffman encoding is not implemented yet.");

        ulong length = await QpackInteger.ReadAsync(stream, 7, first[0], cancellationToken);
        if (length > int.MaxValue)
            throw new InvalidDataException("QPACK string is too long.");

        var bytes = new byte[(int)length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return System.Text.Encoding.UTF8.GetString(bytes);    
    }

    private sealed class PrefixStream(byte[] prefix) : MemoryStream(prefix)
    {
    }
}