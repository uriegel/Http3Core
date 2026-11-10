namespace Http3Core.Protocol;

public static class Http3StreamType
{
    public const ulong Control = 0x00;
    public const ulong Push = 0x01;
    public const ulong QpackEncoder = 0x02;
    public const ulong QpackDecoder = 0x03;
}