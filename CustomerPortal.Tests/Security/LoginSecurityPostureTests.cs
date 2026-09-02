using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CustomerPortal.Data;
using CustomerPortal.Tests.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerPortal.Tests.Security;

/// <summary>
/// NFR-5 / FR-7 / FR-16 / OD-003:A: an unknown email, a wrong password, and a
/// disabled account must be indistinguishable to the caller. Verified at the
/// HTTP level, excluding only the `timestamp` field (api-conventions.md AC-6),
/// which necessarily differs per request and is not part of the
/// anti-enumeration guarantee -- SPEC_REVIEW/PLAN_REVIEW's "byte-for-byte"
/// language refers to every field a client can act on.
/// </summary>
public class LoginSecurityPostureTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LoginSecurityPostureTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostSessions_UnknownEmailWrongPasswordAndDisabledAccount_ProduceIndistinguishableResponses()
    {
        const string knownEmail = "xena@example.com";
        const string disabledEmail = "yara@example.com";
        const string password = "Str0ng&Pass!word";

        await _client.PostAsJsonAsync("/api/v1/customers", new { email = knownEmail, password });
        await _client.PostAsJsonAsync("/api/v1/customers", new { email = disabledEmail, password });
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var disabled = await db.Customers.SingleAsync(c => c.Email == disabledEmail);
            disabled.Enabled = false;
            await db.SaveChangesAsync();
        }

        var unknownEmailResponse = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = "nobody@example.com", password });
        var wrongPasswordResponse = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = knownEmail, password = "Wrong1!Password" });
        var disabledAccountResponse = await _client.PostAsJsonAsync("/api/v1/sessions", new { email = disabledEmail, password });

        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, disabledAccountResponse.StatusCode);

        var unknownBody = await unknownEmailResponse.Content.ReadFromJsonAsync<JsonElement>();
        var wrongBody = await wrongPasswordResponse.Content.ReadFromJsonAsync<JsonElement>();
        var disabledBody = await disabledAccountResponse.Content.ReadFromJsonAsync<JsonElement>();

        AssertIdenticalExceptTimestamp(unknownBody, wrongBody);
        AssertIdenticalExceptTimestamp(unknownBody, disabledBody);

        Assert.False(unknownEmailResponse.Headers.Contains("Set-Cookie"));
        Assert.False(wrongPasswordResponse.Headers.Contains("Set-Cookie"));
        Assert.False(disabledAccountResponse.Headers.Contains("Set-Cookie"));
    }

    private static void AssertIdenticalExceptTimestamp(JsonElement a, JsonElement b)
    {
        Assert.Equal(a.GetProperty("status").GetInt32(), b.GetProperty("status").GetInt32());
        Assert.Equal(a.GetProperty("error").GetString(), b.GetProperty("error").GetString());
        Assert.Equal(a.GetProperty("message").GetString(), b.GetProperty("message").GetString());
        Assert.Equal(a.GetProperty("path").GetString(), b.GetProperty("path").GetString());
        Assert.Equal(a.TryGetProperty("fieldErrors", out _), b.TryGetProperty("fieldErrors", out _));
    }
}
