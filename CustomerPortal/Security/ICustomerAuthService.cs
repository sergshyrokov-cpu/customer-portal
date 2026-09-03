using CustomerPortal.Models.Entities;
using Microsoft.AspNetCore.Http;

namespace CustomerPortal.Security;

public interface ICustomerAuthService
{
    Task<Customer> AuthenticateAndSignInAsync(HttpContext httpContext, string email, string password);
}
