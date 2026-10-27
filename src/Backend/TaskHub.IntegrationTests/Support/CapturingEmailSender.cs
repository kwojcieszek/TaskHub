using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using TaskHub.Application.Dtos.Email;
using TaskHub.Application.Interfaces;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// Collects the messages the API sends, so a test can follow a link as the recipient would.
/// </summary>
public partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public bool IsConfigured => true;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);

        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> SentTo(string address)
    {
        return _sent.Where(message => string.Equals(message.To, address, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>The query parameters of the link in the last message sent to the address.</summary>
    public IDictionary<string, string> LastLinkQueryFor(string address)
    {
        var message = SentTo(address).LastOrDefault();
        Assert.NotNull(message);

        var link = LinkPattern().Match(message.TextBody);
        Assert.True(link.Success, $"The message has no link: {message.TextBody}");

        var query = new Uri(link.Value).Query;

        return QueryHelpers.ParseQuery(query).ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
    }

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex LinkPattern();
}
