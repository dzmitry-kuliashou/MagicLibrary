using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validators;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Books;

public sealed class UpdateBookMetadataCommandValidatorTests
{
    private static UpdateBookMetadataCommand CreateCommand(
        string? title = "Title",
        string? author = null)
    {
        return new UpdateBookMetadataCommand(Guid.NewGuid(), title!, author);
    }

    [Fact]
    public async Task Validate_ValidCommandWithMissingAuthor_Passes()
    {
        var validator = new UpdateBookMetadataCommandValidator();

        var result = await validator.ValidateAsync(CreateCommand());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Validate_BlankTitle_ReturnsMissingTitleFailure(string? title)
    {
        var validator = new UpdateBookMetadataCommandValidator();

        var result = await validator.ValidateAsync(CreateCommand(title));

        var failure = Assert.Single(result.Errors);
        Assert.False(result.IsValid);
        Assert.Equal(nameof(UpdateBookMetadataCommand.Title), failure.PropertyName);
        Assert.Equal(BookErrors.MissingTitle.Code, failure.ErrorCode);
        Assert.Equal(BookErrors.MissingTitle.Description, failure.ErrorMessage);
    }
}
