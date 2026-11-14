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

    public static byte[] EncodeInsertWithNameReferenceStatic(int nameIndex, string value)
    {
        // Validate the static table index.
        QpackStaticTable.Get(nameIndex);

        // Name index uses a 6-bit prefix.
        var result = QpackInteger.Encode((ulong)nameIndex, 6);

        // 1 T NameIndex
        // T = 1 means static table.
        result[0] |= 0xC0;

        // Append the encoded value.
        var encodedValue = QpackString.Encode(value);

        return [.. result, .. encodedValue];
    }

    public static async ValueTask ReadInsertWithNameReferenceStaticAsync(Stream stream, QpackDynamicTable table, CancellationToken cancellationToken = default)
    {
        var first = new byte[1];
        await stream.ReadExactlyAsync(first, cancellationToken);

        // 1 T NameIndex
        if ((first[0] & 0x80) == 0)
            throw new InvalidDataException("Expected Insert With Name Reference instruction.");

        bool isStatic = (first[0] & 0x40) != 0;
        if (!isStatic)
            throw new NotSupportedException("Dynamic table name references are not supported yet.");

        ulong nameIndex = await QpackInteger.ReadAsync(stream, 6, first[0], cancellationToken);
        if (nameIndex >= (ulong)QpackStaticTable.Count)
            throw new InvalidDataException($"Invalid QPACK static table index: {nameIndex}");

        string name = QpackStaticTable.Get((int)nameIndex).Name;
        string value = await QpackString.ReadAsync(stream, cancellationToken);

        table.Insert(name, value);
    }

    public static byte[] EncodeInsertWithoutNameReference(string name, string value)    
    {
        var rawName = System.Text.Encoding.UTF8.GetBytes(name);
        var huffmanName = QpackHuffman.Encode(rawName);

        bool useHuffman = huffmanName.Length < rawName.Length;
        byte[] nameBytes = useHuffman ? huffmanName : rawName;

        // Name length uses a 5-bit prefix.
        var result = QpackInteger.Encode((ulong)nameBytes.Length, 5);

        // 01 H NameLength
        result[0] |= (byte)(0x40 |(useHuffman ? 0x20 : 0x00));
        var encodedValue = QpackString.Encode(value);

        return [.. result, .. nameBytes, .. encodedValue];
    }    
}
