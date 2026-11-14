namespace Http3Core.Protocol;

public readonly record struct QpackFieldSectionPrefix(
    ulong EncodedRequiredInsertCount,
    bool Sign,
    ulong DeltaBase
);