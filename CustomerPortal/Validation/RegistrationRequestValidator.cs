using System.Text;
using CustomerPortal.Models.Requests;
using FluentValidation;

namespace CustomerPortal.Validation;

/// <summary>
/// Spec §6.1/§6.2, security-conventions.md SC-1. Password length is measured
/// in UTF-8 bytes, not characters (spec-review F-5) -- the BCrypt input bound
/// is 72 bytes.
/// </summary>
public class RegistrationRequestValidator : AbstractValidator<RegistrationRequest>
{
    public RegistrationRequestValidator()
    {
        RuleFor(r => r.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

        RuleFor(r => r.Password)
            .NotEmpty()
            .Must(HaveByteLengthBetween12And72)
            .WithMessage("Password does not meet the security policy.")
            .Matches("[A-Z]")
            .WithMessage("Password does not meet the security policy.")
            .Matches("[a-z]")
            .WithMessage("Password does not meet the security policy.")
            .Matches("[0-9]")
            .WithMessage("Password does not meet the security policy.")
            .Matches(@"[^A-Za-z0-9]")
            .WithMessage("Password does not meet the security policy.");
    }

    private static bool HaveByteLengthBetween12And72(string password)
    {
        var byteLength = Encoding.UTF8.GetByteCount(password);
        return byteLength is >= 12 and <= 72;
    }
}
