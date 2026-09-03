using System.Security.Claims;
using CustomerPortal.Exceptions;
using CustomerPortal.Models.Entities;
using CustomerPortal.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace CustomerPortal.Security;

/// <summary>
/// Sole place that calls SignInAsync (security-conventions.md SC-3). Looks up
/// the Customer by email, verifies the password hash, and checks Enabled --
/// all three failure cases (unknown email, wrong password, disabled account)
/// throw the same AuthenticationFailedException (FR-7, FR-16, OD-003:A).
/// A dummy hash-verify call on the unknown-email path keeps its latency
/// comparable to the other two paths (impact-analysis R-2).
/// </summary>
public class CustomerAuthService(ICustomerRepository repository, IPasswordHasher passwordHasher) : ICustomerAuthService
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    public async Task<Customer> AuthenticateAndSignInAsync(HttpContext httpContext, string email, string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await repository.FindByEmailAsync(normalizedEmail);

        var passwordMatches = passwordHasher.Verify(password, customer?.PasswordHash ?? DummyHash);

        if (customer is null || !passwordMatches || !customer.Enabled)
        {
            throw new AuthenticationFailedException();
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new Claim(ClaimTypes.Email, customer.Email),
                new Claim(ClaimTypes.Role, customer.Role),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme));

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return customer;
    }
}
