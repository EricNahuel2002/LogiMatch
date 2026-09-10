using Application.Integrations;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Email;

public class MailKitEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public MailKitEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(
        IReadOnlyCollection<string> recipients,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (recipients.Count == 0)
        {
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.From));
        foreach (var email in recipients)
        {
            message.To.Add(new MailboxAddress(string.Empty, email));
        }
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, _options.UseSsl, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}