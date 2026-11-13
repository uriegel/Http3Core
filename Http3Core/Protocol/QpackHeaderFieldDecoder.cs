namespace Http3Core.Protocol;

public static class QpackHeaderFieldDecoder
{
    public static async ValueTask<QpackStaticTableEntry> ReadIndexedStaticAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var first = new byte[1];
        await stream.ReadExactlyAsync(first, cancellationToken);

        // First two bits must be 11.
        // This identifies an Indexed Field Line using the static table.
        if ((first[0] & 0xC0) != 0xC0)
            throw new InvalidDataException("Expected an indexed static table field.");

        // Decode the index using the remaining six prefix bits.
        ulong index = await QpackInteger.ReadAsync(stream, 6, first[0], cancellationToken);

        if (index >= (ulong)QpackStaticTable.Count)
            throw new InvalidDataException($"Invalid QPACK static table index: {index}");

        return QpackStaticTable.Get((int)index);
    }
    
    public static async ValueTask<QpackHeaderField> ReadLiteralWithNameReferenceAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var first = new byte[1];
        await stream.ReadExactlyAsync(first, cancellationToken);

        // 01 N T NameIndex
        if ((first[0] & 0xC0) != 0x40)
            throw new InvalidDataException("Expected a literal field line with name reference.");

        bool neverIndexed = (first[0] & 0x20) != 0;
        bool isStatic = (first[0] & 0x10) != 0;

        if (!isStatic)
            throw new NotSupportedException("Dynamic table references are not supported yet.");

        ulong index = await QpackInteger.ReadAsync(stream, 4, first[0], cancellationToken);

        if (index >= (ulong)QpackStaticTable.Count)
            throw new InvalidDataException($"Invalid QPACK static table index: {index}");

        string name = QpackStaticTable.Get((int)index).Name;
        string value = await QpackString.ReadAsync(stream, cancellationToken);

        return new QpackHeaderField(name, value, neverIndexed);
    }    
}