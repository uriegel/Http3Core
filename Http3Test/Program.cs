using Http3Core.Protocol;

ulong[] values =
[
    0,
    63,
    64,
    16383,
    16384,
    1073741823,
    1073741824,
    4611686018427387903
];

foreach (var value in values)
{
    await using var stream = new MemoryStream();
    await QuicVariableInteger.WriteAsync(stream, value);
    var encoded = stream.ToArray();
    stream.Position = 0;

    var decoded = await QuicVariableInteger.ReadAsync(stream);
    Console.WriteLine($"{value,22}  {Convert.ToHexString(encoded),16}  {decoded}");

    if (value != decoded)
        throw new Exception("Encoding/decoding error!");
}

await using var frameStream = new MemoryStream();
await Http3FrameWriter.WriteSettingsAsync(frameStream);
var frameBytes = frameStream.ToArray();
Console.WriteLine($"SETTINGS frame: {Convert.ToHexString(frameBytes)}");

if (Convert.ToHexString(frameBytes) != "0400")
    throw new Exception("Invalid SETTINGS frame!");
    
frameStream.Position = 0;
var frame = await Http3FrameReader.ReadAsync(frameStream);

Console.WriteLine($"Frame type: 0x{frame.Type:X}");
Console.WriteLine($"Payload length: {frame.Payload.Length}");    

var settings = new Http3Settings();

settings.Set(Http3Settings.QpackMaxTableCapacity, 4096);
settings.Set(Http3Settings.MaxFieldSectionSize, 65536);
settings.Set(Http3Settings.QpackBlockedStreams, 10);

// Reserved identifier used to test handling of unknown settings.
settings.Set(0x21, 0);

var payload = await settings.EncodePayloadAsync();
Console.WriteLine($"SETTINGS payload: {Convert.ToHexString(payload)}");

var decoded1 = await Http3Settings.DecodePayloadAsync(payload);
foreach (var setting in decoded1.Values)
    Console.WriteLine($"Setting 0x{setting.Key:X} = {setting.Value}");

Console.WriteLine($"Static table entries: {QpackStaticTable.Count}");

var entry = QpackStaticTable.Get(17);
Console.WriteLine($"17: {entry.Name} = {entry.Value}");

entry = QpackStaticTable.Get(25);
Console.WriteLine($"25: {entry.Name} = {entry.Value}");

if (QpackStaticTable.TryGetIndex(":method", "GET", out var index))
    Console.WriteLine($"GET index: {index}");

if (QpackStaticTable.TryGetNameIndex("content-type", out index))
    Console.WriteLine($"content-type name index: {index}");

var intValues = new ulong[]
{
    0, 10, 30, 31, 32, 1337, 16384, 1000000
};

foreach (var value in intValues)
{
    var encoded = QpackInteger.Encode(value, 5);
    Console.WriteLine($"{value,8}: {Convert.ToHexString(encoded)}");
}    

foreach (var value in intValues)
{
    var encoded = QpackInteger.Encode(value, 5);
    using var stream = new MemoryStream(encoded);
    var decoded = await QpackInteger.ReadAsync(stream, 5);
    if (decoded != value)
        throw new Exception($"Mismatch: {value} != {decoded}");
}

Console.WriteLine("All QPACK integer tests passed.");

var original = "content-type";
var stringEncoded = QpackString.Encode(original);
Console.WriteLine(Convert.ToHexString(stringEncoded));

using var memStream = new MemoryStream(stringEncoded);
var stringDecoded = await QpackString.ReadAsync(memStream);
Console.WriteLine(stringDecoded);

var input = System.Text.Encoding.ASCII.GetBytes("www.example.com");
var huffEncoded = QpackHuffman.Encode(input);
Console.WriteLine();
Console.WriteLine(Convert.ToHexString(huffEncoded));
Console.WriteLine();
Console.WriteLine(QpackHuffman.TreeIsBuilt);

var encoded2 = Convert.FromHexString("F1E3C2E5F23A6BA0AB90F4FF");
var decoded2 = QpackHuffman.Decode(encoded2);
Console.WriteLine(System.Text.Encoding.ASCII.GetString(decoded2));

var original1 = System.Text.Encoding.UTF8.GetBytes("Hello HTTP/3!");
var compressed = QpackHuffman.Encode(original1);
var restored = QpackHuffman.Decode(compressed);

Console.WriteLine(original1.SequenceEqual(restored));
Console.WriteLine();

var values2 = new[]
{
    "www.example.com",
    "content-type",
    "Hello HTTP/3!",
    "a",
    ""
};

foreach (var value in values2)
{
    var encoded = QpackString.Encode(value);
    using var stream = new MemoryStream(encoded);
    var decoded = await QpackString.ReadAsync(stream);
    Console.WriteLine($"{value == decoded}: {value} ({Convert.ToHexString(encoded)})");
}

Console.WriteLine();

var encoded3 = QpackHeaderFieldEncoder.EncodeIndexedStatic(17);
Console.WriteLine(Convert.ToHexString(encoded3));
using var stream3 = new MemoryStream(encoded3);
var decoded3 = await QpackHeaderFieldDecoder.ReadIndexedStaticAsync(stream3);
Console.WriteLine($"{decoded3.Name}: {decoded3.Value}");

var encoded4 = QpackHeaderFieldEncoder.EncodeIndexedStatic(98);
Console.WriteLine(Convert.ToHexString(encoded4));