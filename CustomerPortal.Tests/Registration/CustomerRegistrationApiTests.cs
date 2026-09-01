using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CustomerPortal.Tests.Registration;

public class CustomerRegistrationApiTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CustomerRegistrationApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostCustomers_ValidRequest_Returns201WithLocationAndSafeBody()
    {
        var payload = new { email = "frank@example.com", password = "Str0ng&Pass!word" };

        var response = await _client.PostAsJsonAsync("/api/v1/customers", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("frank@example.com", body.GetProperty("email").GetString());
        Assert.Equal("CUSTOMER", body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("password", out _));
        Assert.False(body.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task PostCustomers_DuplicateEmail_Returns409()
    {
        var payload = new { email = "grace@example.com", password = "Str0ng&Pass!word" };
        await _client.PostAsJsonAsync("/api/v1/customers", payload);

        var response = await _client.PostAsJsonAsync("/api/v1/customers", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostCustomers_InvalidEmailFormat_Returns400WithEmailFieldError()
    {
        var payload = new { email = "not-an-email", password = "Str0ng&Pass!word" };

        var response = await _client.PostAsJsonAsync("/api/v1/customers", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(body.GetProperty("fieldErrors").EnumerateArray(),
            e => e.GetProperty("field").GetString() == "email");
    }

    [Fact]
    public async Task PostCustomers_WeakPassword_Returns400WithPasswordFieldError()
    {
        var payload = new { email = "henry@example.com", password = "weak" };

        var response = await _client.PostAsJsonAsync("/api/v1/customers", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(body.GetProperty("fieldErrors").EnumerateArray(),
            e => e.GetProperty("field").GetString() == "password");
    }

    [Fact]
    public async Task PostCustomers_MissingContentType_Returns415()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers")
        {
            Content = new StringContent("{\"email\":\"ivan@example.com\",\"password\":\"Str0ng&Pass!word\"}"),
        };
        request.Content.Headers.ContentType = null;

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task PostCustomers_UnknownJsonField_Returns400()
    {
        var payload = new { email = "julia@example.com", password = "Str0ng&Pass!word", extra = "not-allowed" };

        var response = await _client.PostAsJsonAsync("/api/v1/customers", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
