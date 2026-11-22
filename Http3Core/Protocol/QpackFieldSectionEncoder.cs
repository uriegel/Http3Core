namespace Http3Core.Protocol;

public static class QpackFieldSectionEncoder
{
    public static QpackEncodedFieldSection Encode(ulong requiredInsertCount, ulong baseIndex,
        IReadOnlyList<byte[]> representations, bool referencesDynamicTable)
    {
        // For now, use a positive Delta Base.
        ArgumentOutOfRangeException.ThrowIfLessThan(baseIndex, requiredInsertCount);

        ulong deltaBase = baseIndex - requiredInsertCount;
        byte[] prefix = QpackFieldSectionPrefixEncoder.Encode(requiredInsertCount, sign: false, deltaBase);
        using var result = new MemoryStream();
        result.Write(prefix);
        foreach (var representation in representations)
            result.Write(representation);

        return new QpackEncodedFieldSection(result.ToArray(), referencesDynamicTable);
    }
}