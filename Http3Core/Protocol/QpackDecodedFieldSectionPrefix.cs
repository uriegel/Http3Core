namespace Http3Core.Protocol;

public readonly record struct QpackDecodedFieldSectionPrefix(
    ulong RequiredInsertCount,
    ulong Base
);