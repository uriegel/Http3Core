using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;

var connectionOptions = new QuicClientConnectionOptions
{
    RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 5000),

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

await Task.Delay(2000);

await using var connection =
    await QuicConnection.ConnectAsync(connectionOptions);

Console.WriteLine("QUIC connection established!");
Console.WriteLine($"Remote endpoint: {connection.RemoteEndPoint}");
Console.WriteLine($"Negotiated ALPN: {connection.NegotiatedApplicationProtocol}");

await using var stream =
    await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);

var message = System.Text.Encoding.UTF8.GetBytes("Hello from QUIC client!");

await stream.WriteAsync(message);

stream.CompleteWrites();

var buffer = new byte[1024];

var bytesRead = await stream.ReadAsync(buffer);

var response = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

Console.WriteLine($"Server response: {response}");