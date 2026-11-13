namespace Http3Core.Protocol;

public static class QpackHeaderFieldDecoder
{
    public static async ValueTask<QpackStaticTableEntry> ReadIndexedStaticAsync(Stream stream,CancellationToken cancellationToken = default)
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
}