using CustomerPortal.Models.Dtos;
using CustomerPortal.Models.Requests;
using CustomerPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Controllers;

[ApiController]
[Route("api/v1/sessions")]
public class SessionsController(ICustomerService customerService) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var response = await customerService.LoginAsync(HttpContext, request);
        return Ok(response);
    }
}
