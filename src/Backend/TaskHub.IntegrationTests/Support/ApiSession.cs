using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// One caller of the API: an HTTP client together with the identity it sends, if any.
/// </summary>
public sealed class ApiSession
{
    public ApiSession(HttpClient client)
    {
        Client = client;
    }

    public HttpClient Client { get; }

    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? Token { get; private set; }

    /// <summary>Sends the given access token with every following request.</summary>
    public void Authenticate(string token)
    {
        Token = token;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void SignOut()
    {
        Token = null;
        Client.DefaultRequestHeaders.Authorization = null;
    }

    public Task<HttpResponseMessage> GetAsync(string url)
    {
        return Client.GetAsync(url);
    }

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null)
    {
        return SendAsync(HttpMethod.Post, url, body);
    }

    public Task<HttpResponseMessage> PutAsync(string url, object? body = null)
    {
        return SendAsync(HttpMethod.Put, url, body);
    }

    public Task<HttpResponseMessage> DeleteAsync(string url)
    {
        return Client.DeleteAsync(url);
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: ApiJson.Options);
        }

        return Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> UploadAsync(string url, string fileName, byte[] content, string contentType = "application/octet-stream")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);

        return Client.PostAsync(url, form);
    }

    public async Task<T> GetAsync<T>(string url)
    {
        var response = await GetAsync(url);

        return await response.ReadAsync<T>();
    }

    public async Task<T> PostAsync<T>(string url, object? body = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await PostAsync(url, body);

        return await response.ReadAsync<T>(expected);
    }

    public async Task<T> PutAsync<T>(string url, object? body = null)
    {
        var response = await PutAsync(url, body);

        return await response.ReadAsync<T>();
    }
}
