using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IntegrationTests.Smtp;

public sealed class FakeSmtpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentBag<ReceivedMessage> _messages = new();
    private Task? _acceptLoop;

    public int Port { get; }

    public int AcceptedConnections { get; private set; }

    public bool AuthAttempted { get; private set; }

    public bool AdvertiseAuth { get; init; } = true;

    public IReadOnlyList<ReceivedMessage> ReceivedMessages => _messages.ToList();

    public FakeSmtpServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public void Start()
    {
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            AcceptedConnections++;
            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var connection = client;
        using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, new ASCIIEncoding()) { NewLine = "\r\n", AutoFlush = true };

        await writer.WriteLineAsync("220 FakeSmtp ESMTP ready");

        var inData = false;
        var data = new StringBuilder();

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (inData)
            {
                if (line == ".")
                {
                    inData = false;
                    _messages.Add(new ReceivedMessage(data.ToString()));
                    await writer.WriteLineAsync("250 2.0.0 Message accepted for delivery");
                }
                else
                {
                    data.AppendLine(line);
                }

                continue;
            }

            var verb = line.Split(' ')[0].ToUpperInvariant();
            switch (verb)
            {
                case "EHLO":
                case "HELO":
                    await writer.WriteLineAsync("250-FakeSmtp");
                    await writer.WriteLineAsync("250-8BITMIME");
                    if (AdvertiseAuth)
                    {
                        await writer.WriteLineAsync("250-AUTH PLAIN LOGIN");
                    }

                    await writer.WriteLineAsync("250 OK");
                    break;

                case "MAIL":
                    await writer.WriteLineAsync("250 2.1.0 OK");
                    break;

                case "RCPT":
                    await writer.WriteLineAsync("250 2.1.5 OK");
                    break;

                case "DATA":
                    data.Clear();
                    inData = true;
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    break;

                case "AUTH":
                    AuthAttempted = true;
                    if (line.Contains("LOGIN", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("334 VXNlcm5hbWU6");
                        _ = await reader.ReadLineAsync();
                        await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
                        _ = await reader.ReadLineAsync();
                    }

                    await writer.WriteLineAsync("235 2.7.0 Authentication successful");
                    break;

                case "QUIT":
                    await writer.WriteLineAsync("221 2.0.0 Bye");
                    return;

                case "RSET":
                case "NOOP":
                    await writer.WriteLineAsync("250 OK");
                    break;

                default:
                    await writer.WriteLineAsync("500 5.5.2 Unknown command");
                    break;
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }
}

public sealed record ReceivedMessage(string RawData);