using System.Text;

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
    
    public static async ValueTask<QpackHeaderField> ReadLiteralWithLiteralNameAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var first = new byte[1];
        await stream.ReadExactlyAsync(first, cancellationToken);

        // 001 N H NameLength
        if ((first[0] & 0xE0) != 0x20)
            throw new InvalidDataException("Expected a literal field line with literal name.");

        bool neverIndexed = (first[0] & 0x10) != 0;
        bool huffman = (first[0] & 0x08) != 0;

        ulong nameLength = await QpackInteger.ReadAsync(stream, 3, first[0], cancellationToken);
        if (nameLength > int.MaxValue)
            throw new InvalidDataException("QPACK name is too long.");

        var nameBytes = new byte[(int)nameLength];
        await stream.ReadExactlyAsync(nameBytes, cancellationToken);
        if (huffman)
            nameBytes = QpackHuffman.Decode(nameBytes);

        string name = Encoding.UTF8.GetString(nameBytes);

        string value = await QpackString.ReadAsync(stream, cancellationToken);

        return new QpackHeaderField(name, value, neverIndexed);
    }    
}