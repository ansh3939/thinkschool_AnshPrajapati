using FluentAssertions;
using Quotes;

namespace Quotes.Tests.Unit;

public sealed class CreateQuoteValidatorTests
{
    [Fact]
    public void Validate_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var validator = new CreateQuoteValidator();

        // Act
        var result = validator.Validate("Be kind.", "user-1");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingText_ReturnsTextRequiredError(string? text)
    {
        // Arrange
        var validator = new CreateQuoteValidator();

        // Act
        var result = validator.Validate(text, "user-1");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Quote text is required.");
    }

    [Fact]
    public void Validate_TextLongerThanMax_ReturnsMaxLengthError()
    {
        // Arrange
        var validator = new CreateQuoteValidator();
        var text = new string('a', CreateQuoteValidator.MaxTextLength + 1);

        // Act
        var result = validator.Validate(text, "user-1");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain($"Quote text cannot be longer than {CreateQuoteValidator.MaxTextLength} characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingOwnerId_ReturnsOwnerRequiredError(string? ownerId)
    {
        // Arrange
        var validator = new CreateQuoteValidator();

        // Act
        var result = validator.Validate("Be kind.", ownerId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Owner id is required.");
    }

    [Fact]
    public void Validate_MissingTextAndOwner_ReturnsBothErrors()
    {
        // Arrange
        var validator = new CreateQuoteValidator();

        // Act
        var result = validator.Validate("", "");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Quote text is required.");
        result.Errors.Should().Contain("Owner id is required.");
    }

    [Fact]
    public void Validate_TextAtMaxLength_ReturnsSuccess()
    {
        // Arrange
        var validator = new CreateQuoteValidator();
        var text = new string('a', CreateQuoteValidator.MaxTextLength);

        // Act
        var result = validator.Validate(text, "user-1");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
