using System.Net;
using System.Text.Json;

namespace TaskHub.IntegrationTests.Support;

public static class HttpResponseExtensions
{
    /// <summary>Fails with the response body in the message, so a wrong status explains itself.</summary>
    public static async Task ShouldBeAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var request = response.RequestMessage;

        Assert.Fail($"{request?.Method} {request?.RequestUri?.PathAndQuery} returned {(int)response.StatusCode} {response.StatusCode}, expected {(int)expected} {expected}. Body: {body}");
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        await response.ShouldBeAsync(expected);

        var body = await response.Content.ReadAsStringAsync();
        var value = JsonSerializer.Deserialize<T>(body, ApiJson.Options);

        Assert.NotNull(value);

        return value;
    }

    /// <summary>Reads the { errors: [...] } body every failed request is expected to carry.</summary>
    public static async Task<IReadOnlyList<string>> ReadErrorsAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        await response.ShouldBeAsync(expected);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.True(document.RootElement.TryGetProperty("errors", out var errors), $"The error body has no 'errors' array: {body}");

        var messages = errors.EnumerateArray().Select(error => error.GetString() ?? string.Empty).ToList();

        Assert.NotEmpty(messages);

        return messages;
    }
}
