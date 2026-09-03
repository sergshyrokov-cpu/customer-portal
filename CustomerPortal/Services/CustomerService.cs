using System.Text;
using CustomerPortal.Exceptions;
using CustomerPortal.Models.Dtos;
using CustomerPortal.Models.Entities;
using CustomerPortal.Models.Requests;
using CustomerPortal.Repositories;
using CustomerPortal.Security;
using Microsoft.AspNetCore.Http;

namespace CustomerPortal.Services;

public class CustomerService(
    ICustomerRepository repository,
    IPasswordHasher passwordHasher,
    ICustomerAuthService customerAuthService) : ICustomerService
{
    public async Task<CustomerResponse> RegisterAsync(RegistrationRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await repository.ExistsByEmailAsync(normalizedEmail))
        {
            throw new DuplicateEmailException("An account with this email already exists.");
        }

        if (!IsPolicyCompliant(request.Password))
        {
            throw new InvalidPasswordException("Password does not meet the security policy.");
        }

        var customer = new Customer
        {
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = "CUSTOMER",
            Enabled = true,
        };

        await repository.AddAsync(customer);

        return new CustomerResponse(customer.Id, customer.Email, customer.Role, customer.CreatedAt);
    }

    /// <summary>
    /// Controller-facing entry point for login (plan-review v1 F-1): the
    /// only responsibility here is delegating to ICustomerAuthService and
    /// mapping the returned entity to a DTO (AD-4) -- the authentication
    /// decision itself lives in CustomerAuthService (SC-3).
    /// </summary>
    public async Task<LoginResponse> LoginAsync(HttpContext httpContext, LoginRequest request)
    {
        var customer = await customerAuthService.AuthenticateAndSignInAsync(httpContext, request.Email, request.Password);
        return new LoginResponse(customer.Id, customer.Email, customer.Role);
    }

    /// <summary>
    /// Service-layer re-check (FR-6), independent of the request-layer
    /// FluentValidation rule (Validation/RegistrationRequestValidator).
    /// </summary>
    private static bool IsPolicyCompliant(string password)
    {
        var byteLength = Encoding.UTF8.GetByteCount(password);
        return byteLength is >= 12 and <= 72
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(c => !char.IsLetterOrDigit(c));
    }
}
