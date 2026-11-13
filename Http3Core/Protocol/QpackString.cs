using System.Text;

namespace Http3Core.Protocol;

public static class QpackString
{
    public static byte[] Encode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var huffman = QpackHuffman.Encode(bytes);
        bool useHuffman = huffman.Length < bytes.Length;
        var data = useHuffman ? huffman : bytes;

        using var stream = new MemoryStream();
        var length = QpackInteger.Encode((ulong)data.Length, 7);
        if (useHuffman)
            length[0] |= 0x80;

        stream.Write(length);
        stream.Write(data);

        return stream.ToArray();
    }

    public static async ValueTask WriteAsync(Stream stream, string value, CancellationToken cancellationToken = default)
    {
        var bytes = Encode(value);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    public static async ValueTask<string> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var first = new byte[1];
        await stream.ReadExactlyAsync(first, cancellationToken);

        bool huffman = (first[0] & 0x80) != 0;
        ulong length = await QpackInteger.ReadAsync(stream, 7, first[0], cancellationToken);
        if (length > int.MaxValue)
            throw new InvalidDataException("QPACK string is too long.");

        var bytes = new byte[(int)length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);

        if (huffman)
            bytes = QpackHuffman.Decode(bytes);

        return Encoding.UTF8.GetString(bytes);
    }
    
    private sealed class PrefixStream(byte[] prefix) : MemoryStream(prefix)
    {
    }
}