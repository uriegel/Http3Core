namespace Http3Core.Protocol;

public sealed record Http3Frame(
    ulong Type,
    byte[] Payload
);