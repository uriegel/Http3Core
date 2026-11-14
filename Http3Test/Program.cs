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

Console.WriteLine();

int nameIndex = 44;
var encoded5 = QpackHeaderFieldEncoder.EncodeLiteralWithNameReferenceStatic(nameIndex, "application/json");
Console.WriteLine(Convert.ToHexString(encoded5));
encoded5 = QpackHeaderFieldEncoder.EncodeLiteralWithNameReferenceStatic(nameIndex, "application/json", true);
Console.WriteLine(Convert.ToHexString(encoded5));

Console.WriteLine();

var encoded6 = QpackHeaderFieldEncoder.EncodeLiteralWithNameReferenceStatic(44, "application/json", neverIndexed: true);
await using var stream6 = new MemoryStream(encoded6);
var field = await QpackHeaderFieldDecoder.ReadLiteralWithNameReferenceAsync(stream6);

Console.WriteLine($"Name:          {field.Name}");
Console.WriteLine($"Value:         {field.Value}");
Console.WriteLine($"Never indexed: {field.NeverIndexed}");

Console.WriteLine();

var encoded7 = QpackHeaderFieldEncoder.EncodeLiteralWithLiteralName("x-custom-header", "hello");
Console.WriteLine(Convert.ToHexString(encoded7));
await using var stream7 = new MemoryStream(encoded7);
var field7 = await QpackHeaderFieldDecoder.ReadLiteralWithLiteralNameAsync(stream7);

Console.WriteLine($"Name:          {field7.Name}");
Console.WriteLine($"Value:         {field7.Value}");
Console.WriteLine($"Never indexed: {field7.NeverIndexed}");

Console.WriteLine();

var table = new QpackDynamicTable(4096);
table.SetCapacity(200);

table.Insert("content-type", "application/json");
table.Insert(":method", "GET");
table.Insert("x-custom", "hello");

Console.WriteLine($"Count:       {table.Count}");
Console.WriteLine($"InsertCount: {table.InsertCount}");
Console.WriteLine($"CurrentSize: {table.CurrentSize}");
Console.WriteLine($"Capacity:    {table.Capacity}");

Console.WriteLine(table.GetAbsolute(0).Name);
Console.WriteLine(table.GetAbsolute(1).Name);
Console.WriteLine(table.GetAbsolute(2).Name);

Console.WriteLine(table.GetRelative(0).Name);
Console.WriteLine(table.GetRelative(1).Name);
Console.WriteLine(table.GetRelative(2).Name);

// for (int i = 0; i < table.Count; i++)
// {
//     var entry2 = table.Get(i);
//     Console.WriteLine($"{i}: {entry2.Name}: {entry2.Value}");
// }

Console.WriteLine();

table.SetCapacity(100);
Console.WriteLine($"Count:       {table.Count}");
Console.WriteLine($"CurrentSize: {table.CurrentSize}");
for (int i = 0; i < table.Count; i++)
{
    var entry2 = table.GetRelative(i);
    Console.WriteLine($"{i}: {entry2.Name}: {entry2.Value}");
}

table = new QpackDynamicTable(4096);
table.SetCapacity(4096);

table.Insert("a", "1"); // absolute 0
table.Insert("b", "2"); // absolute 1
table.Insert("c", "3"); // absolute 2
table.Insert("d", "4"); // absolute 3
table.Insert("e", "5"); // absolute 4

int baseIndex = 5;

Console.WriteLine(table.GetRelative(baseIndex, 0).Name);
Console.WriteLine(table.GetRelative(baseIndex, 1).Name);
Console.WriteLine(table.GetRelative(baseIndex, 2).Name);
//Console.WriteLine(table.GetPostBase(baseIndex, 0).Name);

Console.WriteLine();

var encoded8 = QpackFieldSectionPrefixEncoder.Encode(encodedRequiredInsertCount: 5, sign: false, deltaBase: 3);
Console.WriteLine(Convert.ToHexString(encoded8));
await using var stream8 = new MemoryStream(encoded8);
var prefix = await QpackFieldSectionPrefixDecoder.ReadAsync(stream8);
Console.WriteLine($"Required Insert Count: {prefix.EncodedRequiredInsertCount}");
Console.WriteLine($"Sign:                  {prefix.Sign}");
Console.WriteLine($"Delta Base:            {prefix.DeltaBase}");
ulong requiredInsertCount = QpackFieldSectionPrefixDecoder.DecodeRequiredInsertCount(
    encodedRequiredInsertCount: 5, totalNumberOfInserts: 100, maximumTableCapacity: 4096);
Console.WriteLine(requiredInsertCount);

Console.WriteLine();

Console.WriteLine(QpackFieldSectionPrefixDecoder.DecodeBase(requiredInsertCount: 10, sign: false, deltaBase: 3));
Console.WriteLine(QpackFieldSectionPrefixDecoder.DecodeBase(requiredInsertCount: 10, sign: true, deltaBase: 3));

Console.WriteLine();

var encoded9 = QpackFieldSectionPrefixEncoder.Encode(encodedRequiredInsertCount: 5,sign: false, deltaBase: 3);
await using var stream9 = new MemoryStream(encoded9);
var prefix9 = await QpackFieldSectionPrefixDecoder.ReadDecodedAsync(stream9, totalNumberOfInserts: 100, maximumTableCapacity: 4096);

Console.WriteLine($"Required Insert Count: {prefix9.RequiredInsertCount}");
Console.WriteLine($"Base:                  {prefix9.Base}");

Console.WriteLine();

var encoded10 = QpackEncoderStreamInstruction.EncodeSetDynamicTableCapacity(4096);
Console.WriteLine(Convert.ToHexString(encoded10));

Console.WriteLine();

var encoded11 = QpackEncoderStreamInstruction.EncodeSetDynamicTableCapacity(4096);
await using var stream11 = new MemoryStream(encoded11);
ulong capacity = await QpackEncoderStreamInstruction.ReadSetDynamicTableCapacityAsync(stream11);
Console.WriteLine(capacity);

Console.WriteLine();

var table12 = new QpackDynamicTable(4096);
var encoded12 = QpackEncoderStreamInstruction.EncodeSetDynamicTableCapacity(2048);
await using var stream12 = new MemoryStream(encoded12);
await QpackEncoderStreamInstruction.ReadAndApplySetDynamicTableCapacityAsync(stream12, table12);
Console.WriteLine($"Capacity: {table12.Capacity}");

Console.WriteLine();

var encoded13 = QpackEncoderStreamInstruction.EncodeInsertWithNameReferenceStatic(44,"application/json");
Console.WriteLine(Convert.ToHexString(encoded13));

Console.WriteLine();

var table14 = new QpackDynamicTable(4096);
table14.SetCapacity(4096);
var encoded14 = QpackEncoderStreamInstruction.EncodeInsertWithNameReferenceStatic(44, "application/json");
await using var stream14 = new MemoryStream(encoded14);
await QpackEncoderStreamInstruction.ReadInsertWithNameReferenceStaticAsync(stream14, table14);
Console.WriteLine($"Count: {table14.Count}");
Console.WriteLine($"Name:  {table14.GetAbsolute(0).Name}");
Console.WriteLine($"Value: {table14.GetAbsolute(0).Value}");

Console.WriteLine();

var encoded15 = QpackEncoderStreamInstruction.EncodeInsertWithoutNameReference("x-custom-header", "hello");
Console.WriteLine(Convert.ToHexString(encoded15));

Console.WriteLine();

var table16 = new QpackDynamicTable(4096);
table16.SetCapacity(4096);
var encoded16 = QpackEncoderStreamInstruction.EncodeInsertWithoutNameReference("x-custom-header", "hello");
await using var stream16 = new MemoryStream(encoded16);
await QpackEncoderStreamInstruction.ReadInsertWithoutNameReferenceAsync(stream16, table16);
var field16 = table16.GetAbsolute(0);
Console.WriteLine($"Name:  {field16.Name}");
Console.WriteLine($"Value: {field16.Value}");

Console.WriteLine();

var table17 = new QpackDynamicTable(4096);
table17.SetCapacity(4096);
table17.Insert("content-type", "application/json");
table17.Insert("x-custom", "hello");
var encoded17 = QpackEncoderStreamInstruction.EncodeDuplicate(0);
await using var stream17 = new MemoryStream(encoded17);
await QpackEncoderStreamInstruction.ReadDuplicateAsync(stream17, table17);
Console.WriteLine($"InsertCount: {table17.InsertCount}");
var entry17 = table17.GetAbsolute(2);
Console.WriteLine($"{entry17.Name}: {entry17.Value}");

Console.WriteLine();