namespace Http3Core.Protocol;

public static class QpackHeaderFieldEncoder
{
    public static byte[] EncodeIndexedStatic(int index)
    {
        // Validate the static table index.
        QpackStaticTable.Get(index);

        // 11 = Indexed Field Line, T=1 (static table).
        var result = QpackInteger.Encode((ulong)index, 6);
        result[0] |= 0xC0;

        return result;
    }
}