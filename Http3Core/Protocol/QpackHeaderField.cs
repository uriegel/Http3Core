namespace Http3Core.Protocol;

public readonly record struct QpackHeaderField(
    string Name,
    string Value,
    bool NeverIndexed = false
);