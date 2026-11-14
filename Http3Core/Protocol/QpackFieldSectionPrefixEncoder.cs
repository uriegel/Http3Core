namespace Http3Core.Protocol;

public static class QpackFieldSectionPrefixEncoder
{
    public static byte[] Encode(ulong encodedRequiredInsertCount, bool sign, ulong deltaBase)
    {
        var requiredInsertCount = QpackInteger.Encode(encodedRequiredInsertCount, 8);
        var encodedDeltaBase = QpackInteger.Encode(deltaBase, 7);
        encodedDeltaBase[0] |= (byte)(sign ? 0x80 : 0x00);
        return [.. requiredInsertCount, .. encodedDeltaBase];
    }
}