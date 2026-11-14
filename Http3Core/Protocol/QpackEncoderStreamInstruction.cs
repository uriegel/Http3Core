namespace Http3Core.Protocol;

public static class QpackEncoderStreamInstruction
{
    public static byte[] EncodeSetDynamicTableCapacity(ulong capacity)
    {
        var result = QpackInteger.Encode(capacity, 5);
        result[0] |= 0x20; // 001xxxxx
        return result;
    }

    public static async ValueTask<ulong> ReadSetDynamicTableCapacityAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var first = new byte[1];
        await stream.ReadExactlyAsync(first, cancellationToken);

        // 001xxxxx
        if ((first[0] & 0xE0) != 0x20)
            throw new InvalidDataException("Expected Set Dynamic Table Capacity instruction.");

        return await QpackInteger.ReadAsync(stream, 5, first[0], cancellationToken);
    }

    public static async ValueTask ReadAndApplySetDynamicTableCapacityAsync(Stream stream, QpackDynamicTable table, CancellationToken cancellationToken = default)
    {
        ulong capacity = await ReadSetDynamicTableCapacityAsync(stream, cancellationToken);
        if (capacity > int.MaxValue)
            throw new InvalidDataException("Dynamic table capacity is too large.");

        table.SetCapacity((int)capacity);
    }    
}