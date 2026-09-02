using System.Security.Claims;
using CustomerPortal.Exceptions;
using CustomerPortal.Models.Entities;
using CustomerPortal.Repositories;
using CustomerPortal.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerPortal.Tests.Security;

/// <summary>
/// Unit tests for CustomerAuthService (implementation-plan v2 Architectural
/// Changes items 1-3): verifies the uniform-401 anti-enumeration guarantee
/// (FR-7/FR-16), the timing-parity dummy hash-verify call (impact-analysis
/// R-2), and the SignInAsync/claims wiring (SC-2, SC-3), all without a full
/// ASP.NET Core host -- SignInAsync is exercised via a fake IAuthenticationService
/// resolved from a DefaultHttpContext's RequestServices.
/// </summary>
public class CustomerAuthServiceTests
{
    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly Dictionary<string, Customer> _byEmail = new();

        public Task<Customer?> FindByEmailAsync(string email) =>
            Task.FromResult(_byEmail.TryGetValue(email, out var customer) ? customer : null);

        public Task<bool> ExistsByEmailAsync(string email) =>
            Task.FromResult(_byEmail.ContainsKey(email));

        public Task AddAsync(Customer customer)
        {
            _byEmail[customer.Email] = customer;
            return Task.CompletedTask;
        }

        public void Seed(Customer customer) => _byEmail[customer.Email] = customer;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public int VerifyCallCount { get; private set; }

        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string hash)
        {
            VerifyCallCount++;
            return hash == Hash(password);
        }
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public string? SignedInScheme { get; private set; }
        public ClaimsPrincipal? SignedInPrincipal { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            throw new NotSupportedException("Not exercised by AuthenticateAndSignInAsync.");

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException("Not exercised by AuthenticateAndSignInAsync.");

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException("Not exercised by AuthenticateAndSignInAsync.");

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignedInScheme = scheme;
            SignedInPrincipal = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException("Not exercised by AuthenticateAndSignInAsync.");
    }

    private static HttpContext CreateHttpContext(FakeAuthenticationService authenticationService)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authenticationService);
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private static Customer EnabledCustomer(string email, FakePasswordHasher hasher, string password, string role = "CUSTOMER") => new()
    {
        Email = email,
        PasswordHash = hasher.Hash(password),
        Role = role,
        Enabled = true,
    };

    [Fact]
    public async Task AuthenticateAndSignInAsync_UnknownEmail_ThrowsAuthenticationFailedException()
    {
        var sut = new CustomerAuthService(new FakeCustomerRepository(), new FakePasswordHasher());

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            sut.AuthenticateAndSignInAsync(CreateHttpContext(new FakeAuthenticationService()), "nobody@example.com", "whatever12"));
    }

    [Fact]
    public async Task AuthenticateAndSignInAsync_UnknownEmail_StillCallsPasswordVerifyOnceForTimingParity()
    {
        var hasher = new FakePasswordHasher();
        var sut = new CustomerAuthService(new FakeCustomerRepository(), hasher);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            sut.AuthenticateAndSignInAsync(CreateHttpContext(new FakeAuthenticationService()), "nobody@example.com", "whatever12"));

        Assert.Equal(1, hasher.VerifyCallCount);
    }

    [Fact]
    public async Task AuthenticateAndSignInAsync_WrongPassword_ThrowsAuthenticationFailedException()
    {
        var repository = new FakeCustomerRepository();
        var hasher = new FakePasswordHasher();
        repository.Seed(EnabledCustomer("kate@example.com", hasher, "Correct1!Pass"));
        var sut = new CustomerAuthService(repository, hasher);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            sut.AuthenticateAndSignInAsync(CreateHttpContext(new FakeAuthenticationService()), "kate@example.com", "Wrong1!Pass"));
    }

    [Fact]
    public async Task AuthenticateAndSignInAsync_DisabledAccountWithCorrectPassword_ThrowsAuthenticationFailedException()
    {
        var repository = new FakeCustomerRepository();
        var hasher = new FakePasswordHasher();
        var customer = EnabledCustomer("liam@example.com", hasher, "Correct1!Pass");
        customer.Enabled = false;
        repository.Seed(customer);
        var sut = new CustomerAuthService(repository, hasher);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            sut.AuthenticateAndSignInAsync(CreateHttpContext(new FakeAuthenticationService()), "liam@example.com", "Correct1!Pass"));
    }

    [Fact]
    public async Task AuthenticateAndSignInAsync_AllThreeFailureCases_ThrowExceptionWithIdenticalMessage()
    {
        var repository = new FakeCustomerRepository();
        var hasher = new FakePasswordHasher();
        var disabled = EnabledCustomer("mia@example.com", hasher, "Correct1!Pass");
        disabled.Enabled = false;
        repository.Seed(disabled);
        repository.Seed(EnabledCustomer("nina@example.com", hasher, "Correct1!Pass"));
        var sut = new CustomerAuthService(repository, hasher);

        var unknownEmail = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            sut.AuthenticateAndSignInAsync(CreateHttpContext(new FakeAuthenticationService()), "unknown@example.com", "whatever12"));
        var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            sut.AuthenticateAndSignInAsync(CreateHttpContext(new FakeAuthenticationService()), "nina@example.com", "Wrong1!Pass"));
        var disabledAccount = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            sut.AuthenticateAndSignInAsync(CreateHttpContext(new FakeAuthenticationService()), "mia@example.com", "Correct1!Pass"));

        Assert.Equal(unknownEmail.Message, wrongPassword.Message);
        Assert.Equal(unknownEmail.Message, disabledAccount.Message);
    }

    [Fact]
    public async Task AuthenticateAndSignInAsync_ValidCredentials_SignsInWithCorrectRoleClaimAndReturnsCustomer()
    {
        var repository = new FakeCustomerRepository();
        var hasher = new FakePasswordHasher();
        repository.Seed(EnabledCustomer("owen@example.com", hasher, "Correct1!Pass", role: "ADMIN"));
        var sut = new CustomerAuthService(repository, hasher);
        var authenticationService = new FakeAuthenticationService();

        var result = await sut.AuthenticateAndSignInAsync(
            CreateHttpContext(authenticationService), "owen@example.com", "Correct1!Pass");

        Assert.Equal("owen@example.com", result.Email);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, authenticationService.SignedInScheme);
        Assert.NotNull(authenticationService.SignedInPrincipal);
        Assert.Contains(authenticationService.SignedInPrincipal!.Claims, c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
    }

    [Fact]
    public async Task AuthenticateAndSignInAsync_EmailDiffersOnlyByCase_StillAuthenticates()
    {
        var repository = new FakeCustomerRepository();
        var hasher = new FakePasswordHasher();
        repository.Seed(EnabledCustomer("paula@example.com", hasher, "Correct1!Pass"));
        var sut = new CustomerAuthService(repository, hasher);

        var result = await sut.AuthenticateAndSignInAsync(
            CreateHttpContext(new FakeAuthenticationService()), "Paula@Example.com", "Correct1!Pass");

        Assert.Equal("paula@example.com", result.Email);
    }
}
