namespace Http3Core.Protocol;

public sealed record QpackEncodedRepresentation(
    byte[] Data,
    bool ReferencesDynamicTable
);