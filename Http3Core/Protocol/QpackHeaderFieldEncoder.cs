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

    public static byte[] EncodeLiteralWithNameReferenceStatic(int nameIndex, string value, bool neverIndexed = false)
    {
        // Validate the static table index.
        QpackStaticTable.Get(nameIndex);

        // Name reference uses a 4-bit prefix.
        var result = QpackInteger.Encode((ulong)nameIndex, 4);

        // 01 N T NameIndex
        // 01 = Literal Field Line With Name Reference
        // N  = Never Indexed
        // T  = Static Table
        result[0] |= (byte)(
            0x40 |
            0x10 |
            (neverIndexed ? 0x20 : 0x00));

        // Append the literal value.
        var encodedValue = QpackString.Encode(value);

        return [.. result, .. encodedValue];
    }   
}