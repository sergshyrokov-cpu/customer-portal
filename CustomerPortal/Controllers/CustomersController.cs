using CustomerPortal.Models.Dtos;
using CustomerPortal.Models.Requests;
using CustomerPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Controllers;

[ApiController]
[Route("api/v1/customers")]
public class CustomersController(ICustomerService customerService) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<CustomerResponse>> Register([FromBody] RegistrationRequest request)
    {
        var response = await customerService.RegisterAsync(request);
        // GET /api/v1/customers/{id} is not implemented by this Story (api-design v2 §1);
        // the Location value is still the correct, stable target path (FR-8).
        return Created($"/api/v1/customers/{response.Id}", response);
    }
}
