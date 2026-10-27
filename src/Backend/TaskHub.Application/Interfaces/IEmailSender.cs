using TaskHub.Application.Dtos.Email;

namespace TaskHub.Application.Interfaces;

public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
