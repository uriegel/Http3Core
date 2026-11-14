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

    public static ulong DecodeRequiredInsertCount(ulong encodedRequiredInsertCount, ulong totalNumberOfInserts, ulong maximumTableCapacity)
    {
        if (encodedRequiredInsertCount == 0)
            return 0;

        ulong maxEntries = maximumTableCapacity / 32;
        if (maxEntries == 0)
            throw new InvalidDataException("Invalid maximum dynamic table capacity.");

        ulong fullRange = 2 * maxEntries;
        if (encodedRequiredInsertCount > fullRange)
            throw new InvalidDataException("Invalid encoded Required Insert Count.");

        ulong maxValue = totalNumberOfInserts + maxEntries;
        ulong maxWrapped = (maxValue / fullRange) * fullRange;
        ulong requiredInsertCount = maxWrapped + encodedRequiredInsertCount - 1;

        if (requiredInsertCount > maxValue)
            requiredInsertCount -= fullRange;
        if (requiredInsertCount == 0)
            throw new InvalidDataException("Invalid Required Insert Count.");

        return requiredInsertCount;
    }

    public static ulong DecodeBase(ulong requiredInsertCount, bool sign, ulong deltaBase)
    {
        if (sign)
        {
            if (deltaBase >= requiredInsertCount)
                throw new InvalidDataException("Invalid negative Delta Base.");
            return requiredInsertCount - deltaBase - 1;
        }

        return checked(requiredInsertCount + deltaBase);
    }    
    
    public static async ValueTask<QpackDecodedFieldSectionPrefix> ReadDecodedAsync(Stream stream, ulong totalNumberOfInserts, ulong maximumTableCapacity,
        CancellationToken cancellationToken = default)
    {
        var encoded = await ReadAsync(stream, cancellationToken);

        ulong requiredInsertCount = DecodeRequiredInsertCount(encoded.EncodedRequiredInsertCount, totalNumberOfInserts, maximumTableCapacity);
        ulong baseIndex = DecodeBase(requiredInsertCount, encoded.Sign, encoded.DeltaBase);

        return new QpackDecodedFieldSectionPrefix(requiredInsertCount, baseIndex);
}    
}