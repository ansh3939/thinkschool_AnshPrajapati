using QuotesApi.Models;

namespace QuotesApi.Tests;

public class QuoteTests
{
    [Theory]
    [InlineData("A", "Q")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "Q")]
    public void Create_accepts_valid_author_lengths(string author, string text)
    {
        var result = Quote.Create(author, text);

        Assert.True(result.IsSuccess);
        Assert.Equal(author, result.Quote!.Author);
    }

    [Fact]
    public void Create_rejects_empty_author()
    {
        var result = Quote.Create("", "Valid text");

        Assert.False(result.IsSuccess);
        Assert.Equal("author", result.ErrorField);
    }

    [Fact]
    public void Create_rejects_an_author_longer_than_200_characters()
    {
        var result = Quote.Create(new string('A', 201), "Valid text");

        Assert.False(result.IsSuccess);
        Assert.Equal("author", result.ErrorField);
    }

    [Fact]
    public void Create_rejects_empty_text()
    {
        var result = Quote.Create("Valid author", "");

        Assert.False(result.IsSuccess);
        Assert.Equal("text", result.ErrorField);
    }

    [Fact]
    public void Create_rejects_text_longer_than_1000_characters()
    {
        var result = Quote.Create("Valid author", new string('Q', 1001));

        Assert.False(result.IsSuccess);
        Assert.Equal("text", result.ErrorField);
    }

    [Fact]
    public void Create_accepts_a_1000_character_text()
    {
        var text = new string('Q', 1000);

        var result = Quote.Create("A", text);

        Assert.True(result.IsSuccess);
        Assert.Equal(text, result.Quote!.Text);
    }

    [Fact]
    public void Text_has_no_public_setter_after_creation()
    {
        var setter = typeof(Quote).GetProperty(nameof(Quote.Text))!.SetMethod;

        Assert.NotNull(setter);
        Assert.False(setter!.IsPublic);
    }

    [Fact]
    public void Delete_marks_the_existing_quote_as_deleted_without_removing_it()
    {
        var quote = Quote.Create("Author", "Text").Quote!;

        quote.Delete();

        Assert.True(quote.IsDeleted);
        Assert.Equal("Text", quote.Text);
        Assert.Equal("Author", quote.Author);
    }
}
