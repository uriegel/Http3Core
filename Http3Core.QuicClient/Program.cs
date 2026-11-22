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
byte[] instruction1 = QpackEncoderStreamInstruction.EncodeSetDynamicTableCapacity(4096);
byte[] instruction2 = QpackEncoderStreamInstruction.EncodeInsertWithoutNameReference("x-custom-header", "hello");
await encoderStream.WriteAsync(instruction1);
await encoderStream.WriteAsync(instruction2);

var requestStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
byte[] fieldSection = [0x02, 0x00, 0x80];

// Register the dynamic-table reference before sending HEADERS.
http3Connection.RegisterOutstandingFieldSection(requestStream.Id, referencesDynamicTable: true);
http3Connection.RegisterOutstandingFieldSection(requestStream.Id, referencesDynamicTable: true);
await Http3FrameWriter.WriteAsync(requestStream, 0x01, fieldSection);

Console.WriteLine(
    $"Outstanding before ACK: " +
    $"{http3Connection.GetOutstandingFieldSectionCount(requestStream.Id)}");

await http3Connection.CancelQpackStreamAsync(requestStream.Id, cancellation.Token);

await http3Connection.SectionAcknowledged;

Console.WriteLine(
    $"Outstanding after ACK: " +
    $"{http3Connection.GetOutstandingFieldSectionCount(requestStream.Id)}");

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