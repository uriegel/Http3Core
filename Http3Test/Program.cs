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

var table18 = new QpackDynamicTable(4096);
var instructions = new List<byte>();
instructions.AddRange(QpackEncoderStreamInstruction.EncodeSetDynamicTableCapacity(4096));
instructions.AddRange(QpackEncoderStreamInstruction.EncodeInsertWithoutNameReference("x-custom", "hello"));
instructions.AddRange(QpackEncoderStreamInstruction.EncodeDuplicate(0));

await using var stream18 = new MemoryStream(instructions.ToArray());
while (stream18.Position < stream18.Length)
    await QpackEncoderStreamInstruction.ReadAsync(stream18, table18);

Console.WriteLine($"InsertCount: {table18.InsertCount}");

for (int i = 0; i < table18.Count; i++)
{
    var entry18 = table18.GetRelative(i);
    Console.WriteLine($"{i}: {entry18.Name}: {entry18.Value}");
}

Console.WriteLine();

var instructions19 = new List<byte>();

// Set dynamic table capacity to 4096
instructions19.AddRange(QpackEncoderStreamInstruction.EncodeSetDynamicTableCapacity(4096));
// Insert: content-type = application/json
instructions19.AddRange(QpackEncoderStreamInstruction.EncodeInsertWithNameReferenceStatic(44, "application/json"));
// Insert: x-custom-header = hello
instructions19.AddRange(QpackEncoderStreamInstruction.EncodeInsertWithoutNameReference("x-custom-header", "hello"));
// Duplicate the newest entry (relative index 0)
instructions19.AddRange(QpackEncoderStreamInstruction.EncodeDuplicate(0));
using var stream19 = new MemoryStream(instructions19.ToArray());
var table19 = new QpackDynamicTable(4096);
while (stream19.Position < stream19.Length)
    await QpackEncoderStreamInstruction.ReadAsync(stream19, table19);

Console.WriteLine($"InsertCount: {table19.InsertCount}");
Console.WriteLine($"Count: {table19.Count}");

for (int i = 0; i < table19.Count; i++)
{
    var entry19 = table19.GetRelative(i);
    Console.WriteLine($"{i}: {entry19.Name}: {entry19.Value}");
}

Console.WriteLine();

var encoded20 = QpackHeaderFieldEncoder.EncodeIndexedDynamic(0);
Console.WriteLine(Convert.ToHexString(encoded20));

Console.WriteLine();

var table21 = new QpackDynamicTable(4096);
table21.SetCapacity(4096);
table21.Insert("content-type", "application/json");
table21.Insert("x-custom-header", "hello");
int baseIndex21 = table21.InsertCount;

// Encode relative index 0 (newest entry)
byte[] encoded21 = QpackHeaderFieldEncoder.EncodeIndexedDynamic(0);

using var stream21 = new MemoryStream(encoded21);
var field21 = await QpackHeaderFieldDecoder.ReadIndexedDynamicAsync(stream21, table21, baseIndex21);
Console.WriteLine($"{field21.Name}: {field21.Value}");

Console.WriteLine();

byte[] encoded22 = QpackHeaderFieldEncoder.EncodeLiteralWithNameReferenceDynamic(0, "text/plain");
Console.WriteLine(Convert.ToHexString(encoded22));

Console.WriteLine();

var table23 = new QpackDynamicTable(4096);
table23.SetCapacity(4096);
table23.Insert("content-type", "application/json");
table23.Insert("x-custom-header", "hello");
int baseIndex23 = table23.InsertCount;
byte[] encoded23 = QpackHeaderFieldEncoder.EncodeLiteralWithNameReferenceDynamic(0, "world");
using var stream23 = new MemoryStream(encoded23);
var field23 = await QpackHeaderFieldDecoder.ReadLiteralWithNameReferenceDynamicAsync(stream23, table23, baseIndex23);
Console.WriteLine($"{field23.Name}: {field23.Value}");

Console.WriteLine();

var table24 = new QpackDynamicTable(4096);
table24.SetCapacity(4096);
table24.Insert("content-type", "application/json");
table24.Insert("x-custom-header", "hello");
int baseIndex24 = 1;
byte[] encoded24 = QpackHeaderFieldEncoder.EncodeIndexedPostBase(0);
using var stream24 = new MemoryStream(encoded24);
var field24 = await QpackHeaderFieldDecoder.ReadIndexedPostBaseAsync(stream24, table24, baseIndex24);
Console.WriteLine($"{field24.Name}: {field24.Value}");

Console.WriteLine();

var encoded25 = QpackHeaderFieldEncoder.EncodeLiteralWithPostBaseNameReference( 0, "world");
Console.WriteLine(Convert.ToHexString(encoded25));

Console.WriteLine();

var table26 = new QpackDynamicTable(4096);
table26.SetCapacity(4096);
table26.Insert("content-type", "application/json");
table26.Insert("x-custom-header", "hello");
int baseIndex26 = 1;
byte[] encoded26 = QpackHeaderFieldEncoder.EncodeLiteralWithPostBaseNameReference(0, "world");
using var stream26 = new MemoryStream(encoded26);
var field26 = await QpackHeaderFieldDecoder.ReadLiteralWithPostBaseNameReferenceAsync(stream26, table26, baseIndex26);
Console.WriteLine($"{field26.Name}: {field26.Value}");

Console.WriteLine();

bool referencesDynamicTable = false;
var tracker27 = new QpackInsertCountTracker();
var table27 = new QpackDynamicTable(4096);
table27.SetCapacity(4096);
table27.Insert("x-custom-header", "hello");
byte[] section27 = [0x02, 0x00, 0x80];
using var stream27 = new MemoryStream(section27);
var fields27 = await QpackFieldSectionDecoder.DecodeAsync(stream27, table27, tracker27, 1, 4096, () => referencesDynamicTable = true);
foreach (var field27 in fields27)
    Console.WriteLine($"{field27.Name}: {field27.Value}");
Console.WriteLine($"References dynamic table: {referencesDynamicTable}");

Console.WriteLine();

referencesDynamicTable = false;
var tracker28 = new QpackInsertCountTracker();
var section28 = new List<byte> { 0x02, 0x00 };
section28.AddRange(QpackHeaderFieldEncoder.EncodeLiteralWithNameReferenceDynamic(0, "world"));
using var stream28 = new MemoryStream(section28.ToArray());
var fields28 = await QpackFieldSectionDecoder.DecodeAsync(stream28, table27, tracker28, 1, 4096, () => referencesDynamicTable = true);
foreach (var field28 in fields28)
    Console.WriteLine($"{field28.Name}: {field28.Value}");
Console.WriteLine($"References dynamic table: {referencesDynamicTable}");

Console.WriteLine();

referencesDynamicTable = false;
var tracker29 = new QpackInsertCountTracker();
var table29 = new QpackDynamicTable(4096);
table29.SetCapacity(4096);
table29.Insert("content-type", "application/json"); // Absolute index 0
table29.Insert("x-custom-header", "hello");         // Absolute index 1
byte[] section29 = [0x02, 0x00, 0x10];
using var stream29 = new MemoryStream(section29);
var fields29 = await QpackFieldSectionDecoder.DecodeAsync(stream29, table29, tracker29, 1, 4096, () => referencesDynamicTable = true);
foreach (var field29 in fields29)
    Console.WriteLine($"{field29.Name}: {field29.Value}");
Console.WriteLine($"References dynamic table: {referencesDynamicTable}");

Console.WriteLine();

referencesDynamicTable = false;
var tracker30 = new QpackInsertCountTracker();
var table30 = new QpackDynamicTable(4096);
table30.SetCapacity(4096);
table30.Insert("content-type", "application/json");
table30.Insert("x-custom-header", "hello");
var section30 = new List<byte> { 0x02, 0x00 };
section30.AddRange(QpackHeaderFieldEncoder.EncodeLiteralWithPostBaseNameReference(0, "world"));
using var stream30 = new MemoryStream([.. section30]);
var fields30 = await QpackFieldSectionDecoder.DecodeAsync(stream30, table30, tracker30, 1, 4096, () => referencesDynamicTable = true);
foreach (var field30 in fields30)
    Console.WriteLine($"{field30.Name}: {field30.Value}");
Console.WriteLine($"References dynamic table: {referencesDynamicTable}");

Console.WriteLine();

referencesDynamicTable = false;
var tracker31 = new QpackInsertCountTracker();
var table31 = new QpackDynamicTable(4096);
table31.SetCapacity(4096);
byte[] section31 = [0x02, 0x00, 0x80];
using var stream31 = new MemoryStream(section31);
var decodeTask = QpackFieldSectionDecoder.DecodeAsync(stream31, table31, tracker31, 0, 4096, () => referencesDynamicTable = true);
// Simulate an insertion arriving on the encoder stream.
table31.Insert("x-custom-header", "hello");
// Notify waiting field sections.
tracker31.NotifyInsertCountChanged();
var fields31 = await decodeTask;
foreach (var field31 in fields31)
    Console.WriteLine($"{field31.Name}: {field31.Value}");
Console.WriteLine($"References dynamic table: {referencesDynamicTable}");

Console.WriteLine();

byte[] staticSection = [0x00, 0x00, 0xD1];
using var staticStream = new MemoryStream(staticSection);
bool staticReferencesDynamicTable = false;
var staticFields = await QpackFieldSectionDecoder.DecodeAsync(
    staticStream,
    table31,
    tracker31,
    0,
    4096,
    () => staticReferencesDynamicTable = true);
Console.WriteLine($"References dynamic table: {staticReferencesDynamicTable}");

Console.WriteLine();

var table32 = new QpackDynamicTable(128);
table32.SetCapacity(128);
table32.Insert("x-header", "first");
table32.Insert("x-header", "second");
// Force eviction by reducing capacity.
table32.SetCapacity(46);
Console.WriteLine($"InsertCount: {table32.InsertCount}");
Console.WriteLine($"Count: {table32.Count}");
var entry32 = table32.GetAbsolute(1);
Console.WriteLine($"{entry32.Name}: {entry32.Value}");
try
{
    table32.GetAbsolute(0);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);
}

Console.WriteLine();

var tracker33 = new QpackInsertCountTracker();
var table33 = new QpackDynamicTable(4096);
table33.SetCapacity(4096);
var manager33 = new QpackBlockedStreamManager(1, tracker33);
var decodeTask33 = manager33.WaitForInsertCountAsync(0, table33, 1);
Console.WriteLine($"Blocked streams: {manager33.BlockedStreamCount}");
// Simulate receiving an encoder instruction.
table33.Insert("x-custom-header", "hello");
tracker33.NotifyInsertCountChanged();
await decodeTask33;

Console.WriteLine($"Blocked streams: {manager33.BlockedStreamCount}");

Console.WriteLine();

var tracker34 = new QpackInsertCountTracker();
var table34 = new QpackDynamicTable(4096);
table34.SetCapacity(4096);
var manager34 = new QpackBlockedStreamManager(1, tracker34);
// First stream becomes blocked.
var firstWait34 = manager34.WaitForInsertCountAsync(0, table34, 1);
var secondWait = manager34.WaitForInsertCountAsync(0, table34, 1);
Console.WriteLine($"Blocked streams: {manager34.BlockedStreamCount}");
// A second, different stream must not be allowed to block.
try
{
    await manager34.WaitForInsertCountAsync(4, table34, 1);
}
catch (InvalidDataException ex)
{
    Console.WriteLine(ex.Message);
}
Console.WriteLine($"Blocked streams: {manager34.BlockedStreamCount}");
// Simulate receiving an encoder instruction.
table34.Insert("x-custom-header", "hello");
tracker34.NotifyInsertCountChanged();
await firstWait34;
Console.WriteLine($"Blocked streams: {manager34.BlockedStreamCount}");

Console.WriteLine();

Console.WriteLine(Convert.ToHexString(QpackDecoderStreamInstruction.EncodeSectionAcknowledgment(0)));
Console.WriteLine(Convert.ToHexString(QpackDecoderStreamInstruction.EncodeSectionAcknowledgment(4)));
Console.WriteLine(Convert.ToHexString(QpackDecoderStreamInstruction.EncodeSectionAcknowledgment(127)));
Console.WriteLine(Convert.ToHexString(QpackDecoderStreamInstruction.EncodeStreamCancellation(0)));
Console.WriteLine(Convert.ToHexString(QpackDecoderStreamInstruction.EncodeStreamCancellation(4)));
Console.WriteLine(Convert.ToHexString(QpackDecoderStreamInstruction.EncodeStreamCancellation(63)));
Console.WriteLine(Convert.ToHexString(QpackDecoderStreamInstruction.EncodeInsertCountIncrement(1)));
Console.WriteLine(Convert.ToHexString(QpackDecoderStreamInstruction.EncodeInsertCountIncrement(10)));
Console.WriteLine(Convert.ToHexString(QpackDecoderStreamInstruction.EncodeInsertCountIncrement(64)));
byte[] data = QpackDecoderStreamInstruction.EncodeSectionAcknowledgment(127);
using var stream35 = new MemoryStream(data);
long streamId =await QpackDecoderStreamInstruction.ReadSectionAcknowledgmentAsync(stream35);
Console.WriteLine($"Stream ID: {streamId}");
data = QpackDecoderStreamInstruction.EncodeStreamCancellation(127);
using var stream36 = new MemoryStream(data);
long streamId36 = await QpackDecoderStreamInstruction.ReadStreamCancellationAsync(stream36);
Console.WriteLine($"Stream ID: {streamId36}");
data = QpackDecoderStreamInstruction.EncodeInsertCountIncrement(64);
using var stream37 = new MemoryStream(data);
ulong increment = await QpackDecoderStreamInstruction.ReadInsertCountIncrementAsync(stream37);
Console.WriteLine($"Insert Count Increment: {increment}");

data =
[
    .. QpackDecoderStreamInstruction.EncodeSectionAcknowledgment(4),
    .. QpackDecoderStreamInstruction.EncodeStreamCancellation(8),
    .. QpackDecoderStreamInstruction.EncodeInsertCountIncrement(10)
];
using var stream38 = new MemoryStream(data);
while (stream38.Position < stream38.Length)
{
    var instruction = await QpackDecoderStreamInstruction.ReadAsync(stream38);
    Console.WriteLine($"{instruction.Type}: {instruction.Value}");
}

Console.WriteLine();

Console.WriteLine(Convert.ToHexString(QpackDecoderStreamInstruction.EncodeStreamCancellation(4)));

Console.WriteLine();

var manager = new QpackOutstandingStreamManager();
manager.Add(0);
manager.Add(4);
Console.WriteLine(manager.IsOutstanding(0)); // True
Console.WriteLine(manager.IsOutstanding(4)); // True
manager.Acknowledge(0);
Console.WriteLine(manager.IsOutstanding(0)); // False
Console.WriteLine(manager.IsOutstanding(4)); // True
bool cancelled = manager.Cancel(4);
Console.WriteLine(cancelled);                // True
Console.WriteLine(manager.IsOutstanding(4)); // False

Console.WriteLine();

manager = new QpackOutstandingStreamManager();
manager.Add(0);
manager.Add(0);
manager.Add(4);
Console.WriteLine(manager.GetOutstandingCount(0)); // 2
manager.Acknowledge(0);
Console.WriteLine(manager.GetOutstandingCount(0)); // 1
manager.Acknowledge(0);
Console.WriteLine(manager.IsOutstanding(0)); // False
manager.Cancel(4);
Console.WriteLine(manager.IsOutstanding(4)); // False

Console.WriteLine();

manager = new QpackOutstandingStreamManager();
manager.Add(0);
Console.WriteLine(manager.IsOutstanding(0)); // True
manager.Acknowledge(0);
Console.WriteLine(manager.IsOutstanding(0)); // False

Console.WriteLine();

var representations = new[]
{
    QpackHeaderFieldEncoder.EncodeIndexedStaticRepresentation(17)
};
var section = QpackFieldSectionEncoder.Encode(requiredInsertCount: 0, baseIndex: 0, representations, 4096);

Console.WriteLine(Convert.ToHexString(section.Data));
Console.WriteLine(section.ReferencesDynamicTable);

Console.WriteLine();

representations =
[
    QpackHeaderFieldEncoder.EncodeIndexedStaticRepresentation(17),
    QpackHeaderFieldEncoder.EncodeIndexedDynamicRepresentation(0)];
section = QpackFieldSectionEncoder.Encode(requiredInsertCount: 1, baseIndex: 1, representations, 4096);

Console.WriteLine(Convert.ToHexString(section.Data));
Console.WriteLine(section.ReferencesDynamicTable);

Console.WriteLine();

representations =
[
    QpackHeaderFieldEncoder.EncodeIndexedStaticRepresentation(17),
    QpackHeaderFieldEncoder.EncodeIndexedDynamicRepresentation(0),
    QpackHeaderFieldEncoder.EncodeLiteralWithNameReferenceStaticRepresentation(44, "application/json"),
    QpackHeaderFieldEncoder.EncodeLiteralWithNameReferenceDynamicRepresentation(0, "world")
];
section = QpackFieldSectionEncoder.Encode(requiredInsertCount: 1, baseIndex: 1, representations, maximumTableCapacity: 4096);
Console.WriteLine(Convert.ToHexString(section.Data));
Console.WriteLine(section.ReferencesDynamicTable);

Console.WriteLine();

representations =
[
    QpackHeaderFieldEncoder.EncodeIndexedStaticRepresentation(17),
    QpackHeaderFieldEncoder.EncodeLiteralWithLiteralNameRepresentation("x-custom-header", "hello")
];
section = QpackFieldSectionEncoder.Encode(requiredInsertCount: 1, baseIndex: 1, representations, maximumTableCapacity: 4096);
Console.WriteLine(Convert.ToHexString(section.Data));
Console.WriteLine(section.ReferencesDynamicTable);

Console.WriteLine();

byte[] dataFrame = [0x00, 0x03, 0x41, 0x42, 0x43];
using var stream39 = new MemoryStream(dataFrame);
frame = await Http3FrameReader.TryReadAsync(stream39);
Console.WriteLine(frame?.Type); // 0
Console.WriteLine(System.Text.Encoding.ASCII.GetString(frame!.Payload)); // ABC

Console.WriteLine();