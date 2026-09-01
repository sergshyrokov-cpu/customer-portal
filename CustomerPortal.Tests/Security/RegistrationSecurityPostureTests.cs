using System.Net;
using System.Net.Http.Json;
using CustomerPortal.Tests.Registration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerPortal.Tests.Security;

public class RegistrationSecurityPostureTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RegistrationSecurityPostureTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostCustomers_Unauthenticated_IsNotRejectedForLackOfAuthentication()
    {
        var payload = new { email = "karen@example.com", password = "Str0ng&Pass!word" };

        var response = await _client.PostAsJsonAsync("/api/v1/customers", payload);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationFallbackPolicy_RequiresAuthenticatedUser()
    {
        using var scope = _factory.Services.CreateScope();
        var policyProvider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await policyProvider.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task PostCustomers_SuccessfulRegistration_ResponseBodyContainsNoPasswordOrHash()
    {
        var payload = new { email = "leo@example.com", password = "Str0ng&Pass!word" };

        var response = await _client.PostAsJsonAsync("/api/v1/customers", payload);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Str0ng&Pass!word", raw);
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
    }
}
