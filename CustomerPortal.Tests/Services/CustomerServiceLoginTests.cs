using CustomerPortal.Exceptions;
using CustomerPortal.Models.Entities;
using CustomerPortal.Models.Requests;
using CustomerPortal.Repositories;
using CustomerPortal.Security;
using CustomerPortal.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CustomerPortal.Tests.Services;

/// <summary>
/// Unit tests for CustomerService.LoginAsync (implementation-plan v2,
/// plan-review v1 F-1 fix): the sole Controller-facing entry point for
/// login, responsible only for delegating to ICustomerAuthService and
/// mapping the returned Customer entity to LoginResponse (AD-4) -- the
/// authentication decision itself is CustomerAuthServiceTests' concern.
/// </summary>
public class CustomerServiceLoginTests
{
    private sealed class NeverCalledCustomerRepository : ICustomerRepository
    {
        public Task<Customer?> FindByEmailAsync(string email) => throw new InvalidOperationException("Not expected to be called by LoginAsync.");
        public Task<bool> ExistsByEmailAsync(string email) => throw new InvalidOperationException("Not expected to be called by LoginAsync.");
        public Task AddAsync(Customer customer) => throw new InvalidOperationException("Not expected to be called by LoginAsync.");
    }

    private sealed class NeverCalledPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => throw new InvalidOperationException("Not expected to be called by LoginAsync.");
        public bool Verify(string password, string hash) => throw new InvalidOperationException("Not expected to be called by LoginAsync.");
    }

    private sealed class FakeCustomerAuthService : ICustomerAuthService
    {
        private readonly Customer? _customer;
        private readonly Exception? _failure;

        private FakeCustomerAuthService(Customer? customer, Exception? failure)
        {
            _customer = customer;
            _failure = failure;
        }

        public static FakeCustomerAuthService Succeeds(Customer customer) => new(customer, null);
        public static FakeCustomerAuthService Fails(Exception failure) => new(null, failure);

        public Task<Customer> AuthenticateAndSignInAsync(HttpContext httpContext, string email, string password) =>
            _failure is not null ? Task.FromException<Customer>(_failure) : Task.FromResult(_customer!);
    }

    [Fact]
    public async Task LoginAsync_SuccessfulAuthentication_MapsCustomerToLoginResponse()
    {
        var customer = new Customer
        {
            Id = 7,
            Email = "quinn@example.com",
            PasswordHash = "irrelevant-to-this-test",
            Role = "CUSTOMER",
            Enabled = true,
        };
        var service = new CustomerService(
            new NeverCalledCustomerRepository(),
            new NeverCalledPasswordHasher(),
            FakeCustomerAuthService.Succeeds(customer));

        var response = await service.LoginAsync(new DefaultHttpContext(), new LoginRequest("quinn@example.com", "Correct1!Pass"));

        Assert.Equal(7, response.Id);
        Assert.Equal("quinn@example.com", response.Email);
        Assert.Equal("CUSTOMER", response.Role);
    }

    [Fact]
    public async Task LoginAsync_AuthenticationFailure_PropagatesExceptionUnchanged()
    {
        var service = new CustomerService(
            new NeverCalledCustomerRepository(),
            new NeverCalledPasswordHasher(),
            FakeCustomerAuthService.Fails(new AuthenticationFailedException()));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.LoginAsync(new DefaultHttpContext(), new LoginRequest("nobody@example.com", "whatever12")));
    }
}
