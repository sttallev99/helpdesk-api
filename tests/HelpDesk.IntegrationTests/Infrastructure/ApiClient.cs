using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HelpDesk.IntegrationTests.Infrastructure;

/// <summary>Thin wrapper so tests read as "register, login, call" instead of raw HttpClient plumbing.</summary>
public class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public ApiClient(HttpClient client)
    {
        _client = client;
    }

    public void AuthenticateAs(string accessToken)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>Registers a new Customer account, authenticates this client as them, and returns their id.</summary>
    public async Task<(string Token, Guid UserId)> RegisterAsync(string email, string password, string fullName)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, fullName });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;

        AuthenticateAs(token);
        var me = await GetAsync<JsonElement>("/api/v1/auth/me");
        var userId = Guid.Parse(me.GetProperty("id").GetString()!);

        return (token, userId);
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    public Task<HttpResponseMessage> GetRawAsync(string url) => _client.GetAsync(url);

    public async Task<T> GetAsync<T>(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    public Task<HttpResponseMessage> PostRawAsync<TBody>(string url, TBody body) => _client.PostAsJsonAsync(url, body);

    public async Task<T> PostAsync<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    public Task<HttpResponseMessage> PatchRawAsync<TBody>(string url, TBody body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body) };
        return _client.SendAsync(request);
    }
}
