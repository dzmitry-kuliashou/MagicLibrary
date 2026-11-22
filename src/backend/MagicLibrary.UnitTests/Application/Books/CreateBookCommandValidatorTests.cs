using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validators;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Books;

public sealed class CreateBookCommandValidatorTests
{
    private static CreateBookCommand CreateCommand(
        string fileName = "book.pdf",
        string title = "Title",
        string? author = null)
    {
        return new CreateBookCommand(fileName, title, author);
    }

    [Fact]
    public async Task Validate_ValidCommandWithMissingAuthor_Passes()
    {
        var validator = new CreateBookCommandValidator();

        var result = await validator.ValidateAsync(CreateCommand());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("book.txt")]
    [InlineData(@"C:\library\book.pdf")]
    [InlineData("books/book.pdf")]
    [InlineData("../book.pdf")]
    public async Task Validate_InvalidFileName_ReturnsFilenameFailure(string fileName)
    {
        var validator = new CreateBookCommandValidator();

        var result = await validator.ValidateAsync(CreateCommand(fileName));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(CreateBookCommand.FileName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Validate_BlankTitle_ReturnsMissingTitleFailure(string? title)
    {
        var validator = new CreateBookCommandValidator();

        var result = await validator.ValidateAsync(CreateCommand(title: title!));

        var failure = Assert.Single(result.Errors);
        Assert.False(result.IsValid);
        Assert.Equal(nameof(CreateBookCommand.Title), failure.PropertyName);
        Assert.Equal(BookErrors.MissingTitle.Code, failure.ErrorCode);
        Assert.Equal(BookErrors.MissingTitle.Description, failure.ErrorMessage);
    }
}
