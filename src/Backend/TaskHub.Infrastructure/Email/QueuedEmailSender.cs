using System.Threading.Channels;
using Microsoft.Extensions.Options;
using TaskHub.Application.Dtos.Email;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;

namespace TaskHub.Infrastructure.Email;

/// <summary>
/// Hands messages to <see cref="EmailDeliveryService"/> instead of talking to the mail server inside the request,
/// so the time a request takes does not reveal whether a message was sent.
/// </summary>
public class QueuedEmailSender : IEmailSender
{
    private const int Capacity = 1000;

    private readonly Channel<EmailMessage> _queue = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true
    });

    private readonly EmailOptions _options;

    public QueuedEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured => _options.IsConfigured;

    public ChannelReader<EmailMessage> Reader => _queue.Reader;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _queue.Writer.TryWrite(message);

        return Task.CompletedTask;
    }
}
