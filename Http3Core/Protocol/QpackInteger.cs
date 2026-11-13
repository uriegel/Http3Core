namespace Http3Core.Protocol;

public static class QpackInteger
{
    public static byte[] Encode(ulong value, int prefixBits)
    {
        ValidatePrefixBits(prefixBits);
        ulong prefixMax = (1UL << prefixBits) - 1;
        using var stream = new MemoryStream();

        if (value < prefixMax)
            stream.WriteByte((byte)value);
        else
        {
            stream.WriteByte((byte)prefixMax);
            value -= prefixMax;

            while (value >= 128)
            {
                stream.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }

            stream.WriteByte((byte)value);
        }

        return stream.ToArray();
    }

    public static async ValueTask WriteAsync(Stream stream, ulong value, int prefixBits, byte firstByteFlags = 0, CancellationToken cancellationToken = default)
    {
        var bytes = Encode(value, prefixBits);
        bytes[0] |= firstByteFlags;
        await stream.WriteAsync(bytes, cancellationToken);
    }

    public static async ValueTask<ulong> ReadAsync(Stream stream, int prefixBits, CancellationToken cancellationToken = default)
    {
        ValidatePrefixBits(prefixBits);
        var first = new byte[1];
        await stream.ReadExactlyAsync(first, cancellationToken);
        ulong prefixMax = (1UL << prefixBits) - 1;
        ulong value = (ulong)(first[0] & prefixMax);
        if (value < prefixMax)
            return value;

        int shift = 0;

        while (true)
        {
            await stream.ReadExactlyAsync(first, cancellationToken);
            byte current = first[0];
            ulong part = (ulong)(current & 0x7F);
            if (shift >= 63 || part > (ulong.MaxValue - value) >> shift)
                throw new InvalidDataException("QPACK integer overflow.");

            value += part << shift;
            if ((current & 0x80) == 0)
                return value;

            shift += 7;
        }
    }

    static void ValidatePrefixBits(int prefixBits)
    {
        if (prefixBits is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(prefixBits));
    }
}