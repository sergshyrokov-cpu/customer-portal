using CustomerPortal.Models.Requests;
using CustomerPortal.Validation;
using Xunit;

namespace CustomerPortal.Tests.Validation;

public class RegistrationRequestValidatorTests
{
    private readonly RegistrationRequestValidator _validator = new();

    [Fact]
    public void Validate_MissingEmail_IsInvalidWithEmailFieldError()
    {
        var result = _validator.Validate(new RegistrationRequest("", "Str0ng&Pass!word"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrationRequest.Email));
    }

    [Fact]
    public void Validate_InvalidEmailFormat_IsInvalid()
    {
        var result = _validator.Validate(new RegistrationRequest("not-an-email", "Str0ng&Pass!word"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrationRequest.Email));
    }

    [Fact]
    public void Validate_EmailExceeding254Characters_IsInvalid()
    {
        var localPart = new string('a', 250);
        var email = $"{localPart}@example.com";

        var result = _validator.Validate(new RegistrationRequest(email, "Str0ng&Pass!word"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrationRequest.Email));
    }

    [Fact]
    public void Validate_WellFormedEmailWithin254Characters_EmailRuleIsValid()
    {
        var result = _validator.Validate(new RegistrationRequest("valid@example.com", "Str0ng&Pass!word"));

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(RegistrationRequest.Email));
    }

    [Theory]
    [InlineData("short1!A")]
    [InlineData("nouppercase1!aaaa")]
    [InlineData("NOLOWERCASE1!AAAA")]
    [InlineData("NoDigitsHereEither!")]
    [InlineData("NoSpecialCharacter1")]
    public void Validate_PasswordViolatesPolicy_IsInvalidWithPasswordFieldError(string password)
    {
        var result = _validator.Validate(new RegistrationRequest("valid@example.com", password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrationRequest.Password));
    }

    [Fact]
    public void Validate_PasswordMeetsPolicy_PasswordRuleIsValid()
    {
        var result = _validator.Validate(new RegistrationRequest("valid@example.com", "Str0ng&Pass!word"));

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(RegistrationRequest.Password));
    }

    [Fact]
    public void Validate_PasswordExactly72AsciiBytes_IsValid()
    {
        var password = "A1!" + new string('a', 69);

        var result = _validator.Validate(new RegistrationRequest("valid@example.com", password));

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(RegistrationRequest.Password));
    }

    [Fact]
    public void Validate_PasswordMultiByteCharacterPushingByteLengthOver72_IsInvalid()
    {
        // "A1!" + 67 'a' = 70 ASCII bytes/characters, plus '€' (U+20AC, 3 UTF-8 bytes) =
        // 71 characters but 73 bytes -- exercises the byte bound, not the character count
        // (spec-review F-5).
        var password = "A1!" + new string('a', 67) + "€";

        var result = _validator.Validate(new RegistrationRequest("valid@example.com", password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrationRequest.Password));
    }

    [Fact]
    public void Validate_PasswordFailureMessage_NeverEchoesSubmittedValue()
    {
        const string submitted = "weak";

        var result = _validator.Validate(new RegistrationRequest("valid@example.com", submitted));

        Assert.False(result.IsValid);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage.Contains(submitted));
    }
}
