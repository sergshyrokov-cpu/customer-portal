using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerPortal.Data;
using CustomerPortal.Tests.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerPortal.Tests.Login;

public class LoginApiTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LoginApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task RegisterAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/customers", new { email, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task DisableAccountAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = await db.Customers.SingleAsync(c => c.Email == email);
        customer.Enabled = false;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PostSessions_ValidCredentials_Returns200WithSessionCookieAndSafeBody()
    {
        await RegisterAsync("river@example.com", "Str0ng&Pass!word");

        var response = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = "river@example.com", password = "Str0ng&Pass!word" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var cookie = Assert.Single(cookies!);
        Assert.Contains("HttpOnly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", cookie, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("river@example.com", body.GetProperty("email").GetString());
        Assert.Equal("CUSTOMER", body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("password", out _));
        Assert.False(body.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task PostSessions_SuccessfulLogin_ResponseBodyContainsNoPasswordOrHash()
    {
        await RegisterAsync("sage@example.com", "Str0ng&Pass!word");

        var response = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = "sage@example.com", password = "Str0ng&Pass!word" });
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Str0ng&Pass!word", raw);
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostSessions_WrongPassword_Returns401WithNoCookie()
    {
        await RegisterAsync("tara@example.com", "Str0ng&Pass!word");

        var response = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = "tara@example.com", password = "Wrong1!Password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task PostSessions_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = "unknown-user@example.com", password = "whatever12" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task PostSessions_DisabledAccountWithCorrectPassword_Returns401()
    {
        await RegisterAsync("uma@example.com", "Str0ng&Pass!word");
        await DisableAccountAsync("uma@example.com");

        var response = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = "uma@example.com", password = "Str0ng&Pass!word" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task PostSessions_MissingPassword_Returns400WithPasswordFieldError()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = "vince@example.com", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(body.GetProperty("fieldErrors").EnumerateArray(),
            e => e.GetProperty("field").GetString() == "password");
    }

    [Fact]
    public async Task PostSessions_MissingEmail_Returns400WithEmailFieldError()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = "", password = "whatever12" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(body.GetProperty("fieldErrors").EnumerateArray(),
            e => e.GetProperty("field").GetString() == "email");
    }

    [Fact]
    public async Task PostSessions_MissingContentType_Returns415()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions")
        {
            Content = new StringContent("{\"email\":\"walt@example.com\",\"password\":\"whatever12\"}"),
        };
        request.Content.Headers.ContentType = null;

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task PostSessions_MalformedEmailButPresentValue_TreatedAsAuthenticationFailureNot400()
    {
        // Spec §6.1: login does not re-validate email format -- a
        // syntactically-invalid email simply doesn't match any account.
        var response = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = "not-an-email", password = "whatever12" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
