namespace Http3Core.Protocol;

public static class QpackFieldSectionEncoder
{
    public static QpackEncodedFieldSection Encode(ulong requiredInsertCount, ulong baseIndex,
        IReadOnlyList<QpackEncodedRepresentation> representations, int maximumTableCapacity)
    {
        // For now, use a positive Delta Base.
        ArgumentOutOfRangeException.ThrowIfLessThan(baseIndex, requiredInsertCount);

        ulong deltaBase = baseIndex - requiredInsertCount;
        ulong encodedRequiredInsertCount = EncodeRequiredInsertCount(requiredInsertCount, maximumTableCapacity);
        byte[] prefix = QpackFieldSectionPrefixEncoder.Encode(encodedRequiredInsertCount, sign: false, deltaBase);
        
        using var result = new MemoryStream();
        result.Write(prefix);
        foreach (var representation in representations)
            result.Write(representation.Data);
        var referencesDynamicTable = representations.Any(r => r.ReferencesDynamicTable);
        return new QpackEncodedFieldSection(result.ToArray(), referencesDynamicTable);
    }

    static ulong EncodeRequiredInsertCount(ulong requiredInsertCount, int maximumTableCapacity)
    {
        if (requiredInsertCount == 0)
            return 0;

        ulong maxEntries = (ulong)(maximumTableCapacity / 32);
        if (maxEntries == 0)
            throw new ArgumentOutOfRangeException(nameof(maximumTableCapacity));
        ulong fullRange = 2 * maxEntries;

        return requiredInsertCount % fullRange + 1;
    }
}