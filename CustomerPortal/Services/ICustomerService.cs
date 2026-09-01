using CustomerPortal.Models.Dtos;
using CustomerPortal.Models.Requests;

namespace CustomerPortal.Services;

public interface ICustomerService
{
    Task<CustomerResponse> RegisterAsync(RegistrationRequest request);
}
