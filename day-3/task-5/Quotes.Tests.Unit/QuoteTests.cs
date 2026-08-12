using FluentAssertions;
using NSubstitute;
using Quotes;

namespace Quotes.Tests.Unit;

public sealed class QuoteTests
{
    [Fact]
    public void Create_ValidInput_ReturnsQuote()
    {
        // Arrange
        var now = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);

        // Act
        var result = Quote.Create("Stay curious.", "user-1", clock);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Quote.Should().NotBeNull();
        result.Quote!.Text.Should().Be("Stay curious.");
        result.Quote.OwnerId.Should().Be("user-1");
        result.Quote.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void Create_TextWithWhitespace_ReturnsTrimmedQuote()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc));

        // Act
        var result = Quote.Create("  Stay curious.  ", "  user-1  ", clock);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Quote!.Text.Should().Be("Stay curious.");
        result.Quote.OwnerId.Should().Be("user-1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingText_ReturnsFailure(string? text)
    {
        // Arrange
        var clock = Substitute.For<IClock>();

        // Act
        var result = Quote.Create(text, "user-1", clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Quote.Should().BeNull();
        result.Errors.Should().Contain("Quote text is required.");
    }

    [Fact]
    public void Create_TextTooLong_ReturnsFailure()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        var text = new string('x', CreateQuoteValidator.MaxTextLength + 1);

        // Act
        var result = Quote.Create(text, "user-1", clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Quote.Should().BeNull();
        result.Errors.Should().Contain($"Quote text cannot be longer than {CreateQuoteValidator.MaxTextLength} characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingOwnerId_ReturnsFailure(string? ownerId)
    {
        // Arrange
        var clock = Substitute.For<IClock>();

        // Act
        var result = Quote.Create("Stay curious.", ownerId, clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Quote.Should().BeNull();
        result.Errors.Should().Contain("Owner id is required.");
    }

    [Fact]
    public void Create_InvalidTextAndOwner_ReturnsAllValidationErrors()
    {
        // Arrange
        var clock = Substitute.For<IClock>();

        // Act
        var result = Quote.Create("", "", clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Quote text is required.");
        result.Errors.Should().Contain("Owner id is required.");
    }
}
