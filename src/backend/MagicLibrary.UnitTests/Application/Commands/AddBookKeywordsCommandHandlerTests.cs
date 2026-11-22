using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class AddBookKeywordsCommandHandlerTests
{
    private readonly Mock<IBookReadRepository> _readRepository = new();
    private readonly Mock<IBookWriteRepository> _writeRepository = new();

    private AddBookKeywordsCommandHandler CreateHandler()
    {
        return new AddBookKeywordsCommandHandler(
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
            new AddBookKeywordsCommand(bookId, ["keyword"]),
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
    public async Task Handle_ValidCommand_AddsKeywordsAndPersists()
    {
        var book = CreateBook();
        book.AddKeywords(["semantic", "search"]);
        _readRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        _writeRepository
            .Setup(repository => repository.UpdateAsync(book, CancellationToken.None))
            .Returns((Book updatedBook, CancellationToken _) => Task.FromResult(updatedBook));
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddBookKeywordsCommand(book.Id, [" SEMANTIC ", "rag", "Search!!"]),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(3, result.Value.Keywords.Count);
        Assert.Contains("semantic", result.Value.Keywords);
        Assert.Contains("search", result.Value.Keywords);
        Assert.Contains("rag", result.Value.Keywords);
        _writeRepository.Verify(
            repository => repository.UpdateAsync(book, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyKeywordCollection_PersistsUnchangedBook()
    {
        var book = CreateBook();
        book.AddKeywords(["existing"]);
        _readRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        _writeRepository
            .Setup(repository => repository.UpdateAsync(book, CancellationToken.None))
            .Returns((Book updatedBook, CancellationToken _) => Task.FromResult(updatedBook));
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddBookKeywordsCommand(book.Id, []),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(new[] { "existing" }, result.Value.Keywords);
        _writeRepository.Verify(
            repository => repository.UpdateAsync(book, CancellationToken.None),
            Times.Once);
    }
}
