using CustomerPortal.Models.Dtos;
using CustomerPortal.Models.Requests;
using Microsoft.AspNetCore.Http;

namespace CustomerPortal.Services;

public interface ICustomerService
{
    Task<CustomerResponse> RegisterAsync(RegistrationRequest request);

    Task<LoginResponse> LoginAsync(HttpContext httpContext, LoginRequest request);
}
