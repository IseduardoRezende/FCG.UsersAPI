using FCG.Users.Application.DTOs.Users;
using FCG.Users.Application.Validators;
using FluentValidation.TestHelper;

namespace FCG.Users.UnitTests.Validators;

public class RegisterUserDtoValidatorTests
{
    private readonly RegisterUserDtoValidator _validator = new();

    private static RegisterUserDto ValidDto() => new() { Name = "User", Email = "user@test.com", Password = "Abcdef1!" };

    [Fact]
    public void Validate_WithAllFieldsValid_HasNoErrors()
    {
        _validator.TestValidate(ValidDto()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyName_HasNameError()
    {
        var dto = ValidDto();
        dto.Name = "";

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WithNameLongerThan100Characters_HasNameError()
    {
        var dto = ValidDto();
        dto.Name = new string('a', 101);

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("missing-at.com")]
    public void Validate_WithInvalidEmail_HasEmailError(string email)
    {
        var dto = ValidDto();
        dto.Email = email;

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("Ab1!", "shorter than 8 characters")]
    [InlineData("Abcdefgh!", "without number")]
    [InlineData("Abcdefgh1", "without special character")]
    [InlineData("12345678!", "without letter")]
    public void Validate_WithPasswordNotMeetingSecurityPolicy_HasPasswordError(string password, string reason)
    {
        var dto = ValidDto();
        dto.Password = password;

        var result = _validator.TestValidate(dto);

        Assert.False(result.IsValid, $"Password {reason} should be rejected.");
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
