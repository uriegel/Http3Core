namespace Http3Core.Protocol;

public static class QpackEncoderStreamInstruction
{
    public static byte[] EncodeSetDynamicTableCapacity(ulong capacity)
    {
        var result = QpackInteger.Encode(capacity, 5);
        result[0] |= 0x20; // 001xxxxx
        return result;
    }
}