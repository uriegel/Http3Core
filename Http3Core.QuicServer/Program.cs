using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CsTools.Extensions;

var certificate = X509CertificateLoader.LoadPkcs12FromFile(
    Certificate.EncryptDirectory.AppendPath("certificate.pfx"),
    Certificate.GetPfxPassword());

Console.WriteLine($"Certificate loaded: {certificate.Subject}");
Console.WriteLine($"Valid until: {certificate.NotAfter}");

var listenerOptions = new QuicListenerOptions
{
    ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 5000),

    ApplicationProtocols =
    [
        new SslApplicationProtocol("myquic-test")
    ],

    ConnectionOptionsCallback = (_, _, _) =>
        ValueTask.FromResult(new QuicServerConnectionOptions
        {
            ServerAuthenticationOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                ApplicationProtocols =
                [
                    new SslApplicationProtocol("myquic-test")
                ],
                EnabledSslProtocols = SslProtocols.Tls13
            },

            DefaultStreamErrorCode = 0x100,
            DefaultCloseErrorCode = 0x100
        })
};

await using var listener = await QuicListener.ListenAsync(listenerOptions);

Console.WriteLine($"QUIC server listening on {listener.LocalEndPoint}");

while (true)
{
    await using var connection = await listener.AcceptConnectionAsync();
    Console.WriteLine($"Connection established: {connection.RemoteEndPoint}");
    _ = HandleConnectionAsync(connection);
}

static async Task HandleConnectionAsync(QuicConnection connection)
{
    await using (connection)
    {
        Console.WriteLine("Handling connection...");

        var tasks = new List<Task>();
        // Accept three incoming streams
        for (int i = 0; i < 3; i++)
        {
            var stream = await connection.AcceptInboundStreamAsync();
            Console.WriteLine($"Accepted stream {stream.Id}");
            tasks.Add(HandleStreamAsync(stream));
        }

        // Wait until all three streams have been processed
        await Task.WhenAll(tasks);

        Console.WriteLine("All streams completed.");
    }
}

static async Task HandleStreamAsync(QuicStream stream)
{
    await using (stream)
    {
        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer);
        var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        Console.WriteLine($"Stream {stream.Id}: {message}");

        var response = Encoding.UTF8.GetBytes($"Response to: {message}");

        await stream.WriteAsync(response);
        stream.CompleteWrites();
    }
}