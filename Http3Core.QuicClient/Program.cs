using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
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

await using var connection = await QuicConnection.ConnectAsync(connectionOptions);

Console.WriteLine("QUIC connection established!");
Console.WriteLine($"Remote endpoint: {connection.RemoteEndPoint}");
Console.WriteLine($"Negotiated ALPN: {connection.NegotiatedApplicationProtocol}");

// Open the client's HTTP/3 control stream.
await using var controlStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional);

// Identify it as a control stream.
await QuicVariableInteger.WriteAsync(controlStream, Http3StreamType.Control);

// Send initial SETTINGS.
var settings = new Http3Settings();

settings.Set(Http3Settings.QpackMaxTableCapacity, 4096);
settings.Set(Http3Settings.MaxFieldSectionSize, 65536);
settings.Set(Http3Settings.QpackBlockedStreams, 10);
settings.Set(0x21, 0);

await Http3FrameWriter.WriteSettingsAsync(controlStream, settings);

Console.WriteLine("Client control stream opened.");

try
{
    // Temporary QUIC transport test.
    var tasks = Enumerable.Range(1, 3)
        .Select(i => SendMessageAsync(connection, i));

    await Task.WhenAll(tasks);

    Console.WriteLine("All test streams completed.");

    Console.WriteLine("Press ENTER to close the connection.");
    Console.ReadLine();
}
catch (Exception ex)
{
    Console.WriteLine($"Client error: {ex}");
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