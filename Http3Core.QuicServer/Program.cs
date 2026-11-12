using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using CsTools.Extensions;
using Http3Core.Protocol;

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
            MaxInboundUnidirectionalStreams = 3,

            DefaultStreamErrorCode = 0x100,
            DefaultCloseErrorCode = 0x100,

            ServerAuthenticationOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,

                ApplicationProtocols =
                [
                    new SslApplicationProtocol("myquic-test")
                ],

                EnabledSslProtocols = SslProtocols.Tls13
            }
        })
};

await using var listener = await QuicListener.ListenAsync(listenerOptions);

Console.WriteLine($"QUIC server listening on {listener.LocalEndPoint}");

while (true)
{
    var connection = await listener.AcceptConnectionAsync();

    Console.WriteLine($"Connection established: {connection.RemoteEndPoint}");

    _ = HandleConnectionAsync(connection);
}

static async Task HandleConnectionAsync(QuicConnection connection)
{
    await using (connection)
    {
        try
        {
            Console.WriteLine("Handling connection...");

            var http3Connection = new Http3Connection(connection);

            await http3Connection.RunAsync();

            Console.WriteLine("HTTP/3 connection closed.");
        }
        catch (QuicException ex)
        {
            Console.WriteLine($"QUIC error: {ex.QuicError}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex}");
        }
    }
}