namespace Http3Core.Protocol;

public sealed class Http3Settings
{
    public const ulong QpackMaxTableCapacity = 0x01;
    public const ulong MaxFieldSectionSize = 0x06;
    public const ulong QpackBlockedStreams = 0x07;

    public IReadOnlyDictionary<ulong, ulong> Values => values;

    public void Set(ulong identifier, ulong value)
    {
        if (IsReserved(identifier))
            throw new ArgumentException($"Reserved HTTP/3 setting: 0x{identifier:X}", nameof(identifier));
        values[identifier] = value;
    }

    public bool TryGetValue(ulong identifier, out ulong value) => values.TryGetValue(identifier, out value);

    public async ValueTask<byte[]> EncodePayloadAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = new MemoryStream();
        foreach (var setting in values.OrderBy(x => x.Key))
        {
            await QuicVariableInteger.WriteAsync(stream, setting.Key, cancellationToken);
            await QuicVariableInteger.WriteAsync(stream, setting.Value, cancellationToken);
        }

        return stream.ToArray();
    }

    public static async ValueTask<Http3Settings> DecodePayloadAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var settings = new Http3Settings();
        await using var stream = new MemoryStream(payload.ToArray());

        while (stream.Position < stream.Length)
        {
            var identifier = await QuicVariableInteger.ReadAsync(stream, cancellationToken);
            var value = await QuicVariableInteger.ReadAsync(stream, cancellationToken);

            if (IsReserved(identifier))
                throw new InvalidDataException($"Reserved HTTP/3 setting received: 0x{identifier:X}");

            if (settings.values.ContainsKey(identifier))
                throw new InvalidDataException($"Duplicate HTTP/3 setting: 0x{identifier:X}");

            settings.values.Add(identifier, value);
        }

        return settings;
    }

    static bool IsReserved(ulong identifier) => identifier is 0x00 or 0x02 or 0x03 or 0x04 or 0x05;

    readonly Dictionary<ulong, ulong> values = [];
}