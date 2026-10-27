using TaskHub.Application.Dtos.Email;
using TaskHub.Application.Interfaces;

namespace TaskHub.Tests.Support;

/// <summary>
/// Keeps the messages a test caused, in place of the mail server.
/// </summary>
public class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = new();

    public bool IsConfigured { get; set; } = true;

    public IReadOnlyList<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Add(message);

        return Task.CompletedTask;
    }
}
