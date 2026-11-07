using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;

var unidirectional = false;

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

if (unidirectional)
{
    await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional);
    var message = Encoding.UTF8.GetBytes("Hello over a unidirectional stream!");
    await stream.WriteAsync(message, completeWrites: true);
}
else
{
    var tasks = Enumerable.Range(1, 3)
        .Select(i => SendMessageAsync(connection, i));

    await Task.WhenAll(tasks);
}

static async Task SendMessageAsync(QuicConnection connection, int number)
{
    await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
    var message = Encoding.UTF8.GetBytes($"Hello from client, message {number}");
    await stream.WriteAsync(message);
    stream.CompleteWrites();

    using var memory = new MemoryStream();
    var buffer = new byte[1024];

    while (true)
    {
        var bytesRead = await stream.ReadAsync(buffer);
        if (bytesRead == 0)
            break;
        memory.Write(buffer, 0, bytesRead);
    }
    var response = Encoding.UTF8.GetString(memory.ToArray());
    Console.WriteLine($"Stream {stream.Id}: {response}");
}




