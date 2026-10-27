using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TaskHub.Infrastructure.Email;

/// <summary>Sends the messages queued by <see cref="QueuedEmailSender"/>, one after another.</summary>
public class EmailDeliveryService : BackgroundService
{
    private readonly QueuedEmailSender _sender;
    private readonly SmtpEmailTransport _transport;
    private readonly ILogger<EmailDeliveryService> _logger;

    public EmailDeliveryService(QueuedEmailSender sender, SmtpEmailTransport transport, ILogger<EmailDeliveryService> logger)
    {
        _sender = sender;
        _transport = transport;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _sender.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _transport.SendAsync(message, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed message must not stop the delivery of the following ones.
                _logger.LogError(exception, "Sending the e-mail \"{Subject}\" failed.", message.Subject);
            }
        }
    }
}
