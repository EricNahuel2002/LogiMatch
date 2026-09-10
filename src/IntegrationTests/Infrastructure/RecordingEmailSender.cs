using Application.Integrations;

namespace IntegrationTests.Infrastructure;

public sealed class RecordingEmailSender : IEmailSender
{
    public sealed record SentEmail(IReadOnlyCollection<string> Recipients, string Subject, string Body);

    public List<SentEmail> Sent { get; } = [];

    public void Reset() => Sent.Clear();

    public Task SendAsync(
        IReadOnlyCollection<string> recipients,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        Sent.Add(new SentEmail(recipients.ToList(), subject, body));
        return Task.CompletedTask;
    }
}