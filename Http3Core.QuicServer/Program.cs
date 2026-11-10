using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
    var connection = await listener.AcceptConnectionAsync();
    Console.WriteLine($"Connection established: {connection.RemoteEndPoint}");

    var http3Connection = new Http3Connection(connection);
    await http3Connection.RunAsync();    
    // _ = HandleConnectionAsync(connection);
}

async Task HandleConnectionAsync(QuicConnection connection)
{
    await using (connection)
    {
        Console.WriteLine("Handling connection...");

        await using var controlStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional);
        await QuicVariableInteger.WriteAsync(controlStream, 0x00);
        await Http3FrameWriter.WriteSettingsAsync(controlStream);

        Console.WriteLine("Server control stream opened.");

        var tasks = new List<Task>();
        // Accept incoming streams
        while (true)
        {
            try
            {
                var stream = await connection.AcceptInboundStreamAsync();
                Console.WriteLine($"Accepted stream {stream.Id}");
                Console.WriteLine($"Stream type: {stream.Type}");
                var streamType = await QuicVariableInteger.ReadAsync(stream);
                Console.WriteLine($"Stream type: 0x{streamType:X}");       
                var frameType = await QuicVariableInteger.ReadAsync(stream);
                var frameLength = await QuicVariableInteger.ReadAsync(stream);

                Console.WriteLine($"Frame type: 0x{frameType:X}");
                Console.WriteLine($"Frame length: {frameLength}");                         
                if (stream.Type == QuicStreamType.Unidirectional)
                    tasks.Add(HandleUnidirectionalStreamAsync(stream));
                else
                    tasks.Add(HandleStreamAsync(stream));
            }
            catch (QuicException ex)
            {
                Console.WriteLine($"Connection closed: {ex.Message}");
                break;
            }
        }
    }
}

static async Task HandleStreamAsync(QuicStream stream)
{
    await using (stream)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[1024];

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead == 0)
                break;
            memory.Write(buffer, 0, bytesRead);
        }

        var message = Encoding.UTF8.GetString(memory.ToArray());
        Console.WriteLine($"Stream {stream.Id}: {message}");

        var response = Encoding.UTF8.GetBytes($"Response to: {message}");
        await stream.WriteAsync(response);
        stream.CompleteWrites();
    }
}

static async Task HandleUnidirectionalStreamAsync(QuicStream stream)
{
    await using (stream)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[1024];

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer);

            if (bytesRead == 0)
                break;

            memory.Write(buffer, 0, bytesRead);
        }

        var message = Encoding.UTF8.GetString(memory.ToArray());

        Console.WriteLine($"Unidirectional stream {stream.Id}: {message}");
    }
}   

