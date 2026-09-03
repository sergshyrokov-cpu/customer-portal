using CustomerPortal.Models.Requests;
using CustomerPortal.Validation;
using Xunit;

namespace CustomerPortal.Tests.Validation;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_MissingEmail_IsInvalidWithEmailFieldError()
    {
        var result = _validator.Validate(new LoginRequest("", "whatever12"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Email));
    }

    [Fact]
    public void Validate_MissingPassword_IsInvalidWithPasswordFieldError()
    {
        var result = _validator.Validate(new LoginRequest("skye@example.com", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Password));
    }

    [Fact]
    public void Validate_MalformedButNonBlankEmail_IsValid()
    {
        // Spec §6.1: login deliberately does NOT re-validate email format -- an
        // invalid-shaped-but-present email simply fails to match any account
        // later (401), not a 400.
        var result = _validator.Validate(new LoginRequest("not-an-email", "whatever12"));

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(LoginRequest.Email));
    }

    [Fact]
    public void Validate_ShortPasswordFailingRegistrationPolicy_IsValid()
    {
        // Spec §6.2: login deliberately does NOT re-check password policy -- a
        // policy-violating value simply fails to match any real hash later
        // (401), not a 400.
        var result = _validator.Validate(new LoginRequest("skye@example.com", "weak"));

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(LoginRequest.Password));
    }

    [Fact]
    public void Validate_NonBlankEmailAndPassword_IsValid()
    {
        var result = _validator.Validate(new LoginRequest("skye@example.com", "whatever12"));

        Assert.True(result.IsValid);
    }
}
