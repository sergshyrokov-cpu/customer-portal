using CustomerPortal.Models.Requests;
using FluentValidation;

namespace CustomerPortal.Validation;

/// <summary>
/// Spec §6.1/§6.2: login deliberately does NOT re-validate email format or
/// password policy -- a malformed/policy-violating value simply fails to
/// match any account/hash later (401), not a 400, to avoid a
/// validation-based side channel.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty();
        RuleFor(r => r.Password).NotEmpty();
    }
}
