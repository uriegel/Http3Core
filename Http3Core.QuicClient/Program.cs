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

await http3Connection.Initialized;

var encoderStream = http3Connection.QpackEncoderStream!;
Console.WriteLine($"Client encoder stream ID: {encoderStream.Id}");
byte[] instruction1 = QpackEncoderStreamInstruction.EncodeSetDynamicTableCapacity(4096);
byte[] instruction2 = QpackEncoderStreamInstruction.EncodeInsertWithoutNameReference("x-custom-header", "hello");
Console.WriteLine($"Writing capacity: {Convert.ToHexString(instruction1)}");
await encoderStream.WriteAsync(instruction1);
Console.WriteLine($"Writing insertion: {Convert.ToHexString(instruction2)}");
await encoderStream.WriteAsync(instruction2);

Console.WriteLine("QPACK encoder instructions written.");

var requestStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
byte[] fieldSection = [0x02, 0x00, 0x80];

// Register the dynamic-table reference before sending HEADERS.
http3Connection.RegisterOutstandingFieldSection(requestStream.Id, referencesDynamicTable: true);
await Http3FrameWriter.WriteAsync(requestStream, 0x01, fieldSection);

Console.WriteLine(
    $"Outstanding before ACK: " +
    $"{http3Connection.GetOutstandingFieldSectionCount(requestStream.Id)}");

// Signal that the request is complete.
requestStream.CompleteWrites();

// Wait for the server's Section Acknowledgment.
await http3Connection.SectionAcknowledged;

Console.WriteLine(
    $"Outstanding after ACK: " +
    $"{http3Connection.GetOutstandingFieldSectionCount(requestStream.Id)}");

Console.WriteLine("HTTP/3 connection closed.");