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