using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using Http3Core.Protocol;

var connectionOptions = new QuicClientConnectionOptions
{
    RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 5000),

    MaxInboundUnidirectionalStreams = 3,

    DefaultStreamErrorCode = 0x100,
    DefaultCloseErrorCode = 0x100,

    ClientAuthenticationOptions = new SslClientAuthenticationOptions
    {
        TargetHost = "uriegel.de",

        ApplicationProtocols =
        [
            new SslApplicationProtocol("myquic-test")
        ],

        EnabledSslProtocols = SslProtocols.Tls13
    }
};

Console.WriteLine("Connecting to QUIC server...");

await using var connection = await QuicConnection.ConnectAsync(connectionOptions);

Console.WriteLine("QUIC connection established!");
Console.WriteLine($"Remote endpoint: {connection.RemoteEndPoint}");
Console.WriteLine($"Negotiated ALPN: {connection.NegotiatedApplicationProtocol}");

// Start HTTP/3 connection management.
using var cancellation = new CancellationTokenSource();

var http3Connection = new Http3Connection(connection);
var http3Task = http3Connection.RunAsync(cancellation.Token);

Console.WriteLine("HTTP/3 connection started.");

var encoderStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional);
await QuicVariableInteger.WriteAsync(encoderStream, Http3StreamType.QpackEncoder);
Console.WriteLine("Client QPACK encoder stream opened.");

byte[] instruction1 = QpackEncoderStreamInstruction.EncodeSetDynamicTableCapacity(4096);
byte[] instruction2 = QpackEncoderStreamInstruction.EncodeInsertWithoutNameReference("x-custom-header", "hello");
await encoderStream.WriteAsync(instruction1);
await encoderStream.WriteAsync(instruction2);

QuicStream controlStream = null!;
QuicStream peerEncoderStream = null!;
QuicStream decoderStream = null!;
for (int i = 0; i < 3; i++)
{
    var stream = await connection.AcceptInboundStreamAsync();
    ulong streamType = await QuicVariableInteger.ReadAsync(stream);
    Console.WriteLine($"Received stream type: {streamType}");
    switch (streamType)
    {
        case Http3StreamType.Control:
            controlStream = stream;
            break;

        case Http3StreamType.QpackEncoder:
            peerEncoderStream = stream;
            break;

        case Http3StreamType.QpackDecoder:
            decoderStream = stream;
            break;
    }
}
var instruction = await QpackDecoderStreamInstruction.ReadAsync(decoderStream!);
Console.WriteLine($"{instruction.Type}: {instruction.Value}");

Console.WriteLine("Press ENTER to close the connection.");
Console.ReadLine();

// Stop HTTP/3 connection management.
cancellation.Cancel();

try
{
    await http3Task;
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}

Console.WriteLine("HTTP/3 connection closed.");