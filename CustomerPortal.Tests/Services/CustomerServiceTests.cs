using CustomerPortal.Exceptions;
using CustomerPortal.Models.Entities;
using CustomerPortal.Models.Requests;
using CustomerPortal.Repositories;
using CustomerPortal.Security;
using CustomerPortal.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CustomerPortal.Tests.Services;

public class CustomerServiceTests
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
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string hash) => hash == Hash(password);
    }

    /// <summary>
    /// CustomerService's constructor gained a third dependency, ICustomerAuthService,
    /// for LoginAsync (implementation-plan v2 / plan-review v1 F-1). RegisterAsync
    /// never touches it -- this stub only exists so these existing tests keep
    /// compiling against the new constructor shape; it throws if ever invoked.
    /// </summary>
    private sealed class NeverCalledCustomerAuthService : ICustomerAuthService
    {
        public Task<Customer> AuthenticateAndSignInAsync(HttpContext httpContext, string email, string password) =>
            throw new InvalidOperationException("Not expected to be called by RegisterAsync.");
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesEnabledCustomerWithCustomerRole()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository, new FakePasswordHasher(), new NeverCalledCustomerAuthService());

        var response = await service.RegisterAsync(new RegistrationRequest("alice@example.com", "Str0ng&Pass!word"));

        Assert.Equal("alice@example.com", response.Email);
        Assert.Equal("CUSTOMER", response.Role);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_PersistsHashedPasswordNotPlaintext()
    {
        var repository = new FakeCustomerRepository();
        var hasher = new FakePasswordHasher();
        var service = new CustomerService(repository, hasher, new NeverCalledCustomerAuthService());

        await service.RegisterAsync(new RegistrationRequest("bob@example.com", "Str0ng&Pass!word"));

        var stored = await repository.FindByEmailAsync("bob@example.com");
        Assert.NotNull(stored);
        Assert.NotEqual("Str0ng&Pass!word", stored!.PasswordHash);
        Assert.Equal(hasher.Hash("Str0ng&Pass!word"), stored.PasswordHash);
    }

    [Fact]
    public async Task RegisterAsync_EmailDiffersOnlyByCase_NormalizedToLowercaseBeforeStorage()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository, new FakePasswordHasher(), new NeverCalledCustomerAuthService());

        await service.RegisterAsync(new RegistrationRequest("Carol@Example.com", "Str0ng&Pass!word"));

        Assert.True(await repository.ExistsByEmailAsync("carol@example.com"));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmailCaseInsensitive_ThrowsDuplicateEmailException()
    {
        var repository = new FakeCustomerRepository();
        repository.Seed(new Customer
        {
            Email = "dave@example.com",
            PasswordHash = "existing-hash",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        var service = new CustomerService(repository, new FakePasswordHasher(), new NeverCalledCustomerAuthService());

        await Assert.ThrowsAsync<DuplicateEmailException>(() =>
            service.RegisterAsync(new RegistrationRequest("Dave@Example.com", "Str0ng&Pass!word")));
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_SetsEnabledTrue()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository, new FakePasswordHasher(), new NeverCalledCustomerAuthService());

        await service.RegisterAsync(new RegistrationRequest("erin@example.com", "Str0ng&Pass!word"));

        var stored = await repository.FindByEmailAsync("erin@example.com");
        Assert.True(stored!.Enabled);
    }
}
