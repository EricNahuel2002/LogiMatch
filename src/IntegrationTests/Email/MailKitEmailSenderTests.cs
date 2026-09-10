using Infrastructure.Email;
using IntegrationTests.Smtp;
using Microsoft.Extensions.Options;
using System.Net.Sockets;

namespace IntegrationTests.Email;

public class MailKitEmailSenderTests
{
    [Fact]
    public async Task SendAsync_DeliversMessageToAllRecipients()
    {
        using var server = new FakeSmtpServer();
        server.Start();

        var sender = CreateSender(server.Port);

        await sender.SendAsync(
            ["a@test.com", "b@test.com"],
            "Asunto de prueba",
            "Cuerpo del mensaje",
            CancellationToken.None);

        var message = Assert.Single(server.ReceivedMessages);
        Assert.Contains("From: LogiMatch <no-reply@logimatch.com>", message.RawData);
        Assert.Contains("To: a@test.com, b@test.com", message.RawData);
        Assert.Contains("Subject: Asunto de prueba", message.RawData);
        Assert.Contains("Cuerpo del mensaje", message.RawData);
    }

    [Fact]
    public async Task SendAsync_EmptyRecipients_DoesNotOpenConnection()
    {
        using var server = new FakeSmtpServer();
        server.Start();

        var sender = CreateSender(server.Port);

        await sender.SendAsync([], "Asunto", "Cuerpo", CancellationToken.None);

        Assert.Equal(0, server.AcceptedConnections);
        Assert.Empty(server.ReceivedMessages);
    }

    [Fact]
    public async Task SendAsync_WithCredentials_SendsAuthCommand()
    {
        using var server = new FakeSmtpServer();
        server.Start();

        var options = Options.Create(new EmailOptions
        {
            Host = "127.0.0.1",
            Port = server.Port,
            Username = "user",
            Password = "pass",
            From = "no-reply@logimatch.com",
            FromName = "LogiMatch",
            UseSsl = false
        });
        var sender = new MailKitEmailSender(options);

        await sender.SendAsync(["a@test.com"], "Asunto", "Cuerpo", CancellationToken.None);

        Assert.True(server.AuthAttempted);
        Assert.Single(server.ReceivedMessages);
    }

    [Fact]
    public async Task SendAsync_WhenServerUnreachable_Throws()
    {
        var closedPort = PortOfDisposedServer();

        var sender = CreateSender(closedPort);

        await Assert.ThrowsAsync<SocketException>(() =>
            sender.SendAsync(["a@test.com"], "Asunto", "Cuerpo", CancellationToken.None));
    }

    private static MailKitEmailSender CreateSender(int port)
    {
        var options = Options.Create(new EmailOptions
        {
            Host = "127.0.0.1",
            Port = port,
            From = "no-reply@logimatch.com",
            FromName = "LogiMatch",
            UseSsl = false
        });

        return new MailKitEmailSender(options);
    }

    private static int PortOfDisposedServer()
    {
        var server = new FakeSmtpServer();
        server.Start();
        var port = server.Port;
        server.Dispose();
        return port;
    }
}