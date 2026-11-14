namespace Http3Core.Protocol;

public static class QpackFieldSectionPrefixDecoder
{
    public static async ValueTask<QpackFieldSectionPrefix> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ulong encodedRequiredInsertCount = await QpackInteger.ReadAsync(stream, 8, cancellationToken);

        var first = new byte[1];
        await stream.ReadExactlyAsync(first, cancellationToken);

        bool sign = (first[0] & 0x80) != 0;
        ulong deltaBase = await QpackInteger.ReadAsync(stream, 7, first[0], cancellationToken);
        return new QpackFieldSectionPrefix(encodedRequiredInsertCount, sign, deltaBase);
    }
}