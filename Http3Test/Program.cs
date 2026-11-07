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