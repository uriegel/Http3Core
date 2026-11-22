namespace Http3Core.Protocol;

public sealed record QpackEncodedFieldSection(
    byte[] Data,
    bool ReferencesDynamicTable
);