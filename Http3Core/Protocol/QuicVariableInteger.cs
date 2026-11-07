namespace Http3Core.Protocol;

public static class QuicVariableInteger
{
    public static byte[] Encode(ulong value)
    {
        int length = value switch
        {
            <= 0x3F => 1,
            <= 0x3FFF => 2,
            <= 0x3FFF_FFFF => 4,
            <= MaxValue => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

        byte prefix = length switch
        {
            1 => 0x00,
            2 => 0x40,
            4 => 0x80,
            8 => 0xC0,
            _ => throw new InvalidOperationException()
        };

        var result = new byte[length];

        for (int i = length - 1; i >= 0; i--)
        {
            result[i] = (byte)(value & 0xFF);
            value >>= 8;
        }

        result[0] |= prefix;

        return result;
    }

    public static async ValueTask WriteAsync(Stream stream, ulong value, CancellationToken cancellationToken = default)
    {
        var bytes = Encode(value);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    public static async ValueTask<ulong> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var firstByte = new byte[1];
        await stream.ReadExactlyAsync(firstByte, cancellationToken);
        int length = 1 << (firstByte[0] >> 6);
        var bytes = new byte[length];
        bytes[0] = (byte)(firstByte[0] & 0x3F);

        if (length > 1)
            await stream.ReadExactlyAsync(bytes.AsMemory(1), cancellationToken);

        ulong value = 0;
        foreach (byte b in bytes)
            value = (value << 8) | b;

        return value;
    }
    
    const ulong MaxValue = 0x3FFF_FFFF_FFFF_FFFF;
}    
