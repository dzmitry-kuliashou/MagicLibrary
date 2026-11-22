using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class UpdateBookMetadataCommandHandlerTests
{
    private readonly Mock<IValidator<UpdateBookMetadataCommand>> _validator = new();
    private readonly Mock<IBookReadRepository> _readRepository = new();
    private readonly Mock<IBookWriteRepository> _writeRepository = new();

    private UpdateBookMetadataCommandHandler CreateHandler()
    {
        _validator
            .Setup(validator => validator.ValidateAsync(
                It.IsAny<UpdateBookMetadataCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        return new UpdateBookMetadataCommandHandler(
            _validator.Object,
            _readRepository.Object,
            _writeRepository.Object);
    }

    private static Book CreateBook()
    {
        return Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
    }

    [Fact]
    public async Task Handle_BookDoesNotExist_ReturnsNotFound()
    {
        var bookId = Guid.NewGuid();
        _readRepository
            .Setup(repository => repository.GetByIdAsync(bookId, CancellationToken.None))
            .ReturnsAsync((Book?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UpdateBookMetadataCommand(bookId, "Title", null),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(BookErrors.BookNotFound(bookId).Code, error.Code);
        _writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Book>(),
                CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesMetadataAndPersists()
    {
        var book = CreateBook();
        _readRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        _writeRepository
            .Setup(repository => repository.UpdateAsync(book, CancellationToken.None))
            .Returns((Book updatedBook, CancellationToken _) => Task.FromResult(updatedBook));
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UpdateBookMetadataCommand(book.Id, "Updated title", null),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("Updated title", result.Value.Title);
        Assert.Equal(string.Empty, result.Value.Author);
        _writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<Book>(updatedBook =>
                    updatedBook.Id == book.Id
                    && updatedBook.Title == "Updated title"
                    && updatedBook.Author == string.Empty),
                CancellationToken.None),
            Times.Once);
    }
}
