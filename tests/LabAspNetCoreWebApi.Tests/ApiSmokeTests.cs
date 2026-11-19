using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LabAspNetCoreWebApi.Tests;

public class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiSmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Swagger_ui_is_available_in_development()
    {
        HttpResponseMessage response = await _client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_describes_each_operation()
    {
        HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Liveness probe", body, StringComparison.Ordinal);
        Assert.Contains("Issue a lab JWT", body, StringComparison.Ordinal);
        Assert.Contains("Require the admin role", body, StringComparison.Ordinal);
        Assert.Contains("Block a request thread on purpose", body, StringComparison.Ordinal);
        Assert.Contains("Process liveness", body, StringComparison.Ordinal);
        Assert.Contains("\"userName\"", body, StringComparison.Ordinal);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement adminSecurity = document.RootElement
            .GetProperty("paths")
            .GetProperty("/lab/admin")
            .GetProperty("get")
            .GetProperty("security");
        Assert.Contains("Bearer", adminSecurity.ToString(), StringComparison.Ordinal);
        Assert.False(
            document.RootElement.GetProperty("paths").GetProperty("/health").GetProperty("get").TryGetProperty("security", out _));
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        HttpResponseMessage response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Di_endpoint_shows_lifetime_behavior()
    {
        HttpResponseMessage response = await _client.GetAsync("/lab/di");
        _ = response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string transient0 = doc.RootElement.GetProperty("transient")[0].GetString()!;
        string transient1 = doc.RootElement.GetProperty("transient")[1].GetString()!;
        string scoped0 = doc.RootElement.GetProperty("scoped")[0].GetString()!;
        string scoped1 = doc.RootElement.GetProperty("scoped")[1].GetString()!;
        Assert.NotEqual(transient0, transient1);
        Assert.Equal(scoped0, scoped1);
    }

    [Fact]
    public async Task Token_without_body_returns_400()
    {
        using HttpRequestMessage message = new(HttpMethod.Post, "/lab/token");
        HttpResponseMessage response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("userName", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Token_with_invalid_json_returns_400()
    {
        using HttpRequestMessage message = new(HttpMethod.Post, "/lab/token");
        message.Content = new StringContent("{", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("unexpected error", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Secure_endpoint_requires_token_then_succeeds()
    {
        HttpResponseMessage unauthorized = await _client.GetAsync("/lab/secure");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        HttpResponseMessage tokenResponse = await _client.PostAsJsonAsync(
            "/lab/token",
            new { userName = "sevann", role = "admin" });
        _ = tokenResponse.EnsureSuccessStatusCode();
        using JsonDocument tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        string accessToken = tokenDoc.RootElement.GetProperty("accessToken").GetString()!;

        using HttpRequestMessage secureRequest = new(HttpMethod.Get, "/lab/secure");
        secureRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage secure = await _client.SendAsync(secureRequest);
        Assert.Equal(HttpStatusCode.OK, secure.StatusCode);

        using HttpRequestMessage adminRequest = new(HttpMethod.Get, "/lab/admin");
        adminRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage admin = await _client.SendAsync(adminRequest);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    public async Task Admin_endpoint_rejects_reader_token()
    {
        HttpResponseMessage tokenResponse = await _client.PostAsJsonAsync(
            "/lab/token",
            new { userName = "sevann", role = "reader" });
        _ = tokenResponse.EnsureSuccessStatusCode();
        using JsonDocument tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        string accessToken = tokenDoc.RootElement.GetProperty("accessToken").GetString()!;

        using HttpRequestMessage adminRequest = new(HttpMethod.Get, "/lab/admin");
        adminRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage admin = await _client.SendAsync(adminRequest);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
    }

    [Fact]
    public async Task Boom_returns_problem_details()
    {
        HttpResponseMessage response = await _client.GetAsync("/lab/boom");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ProblemDetails", body, StringComparison.OrdinalIgnoreCase);
    }
}
