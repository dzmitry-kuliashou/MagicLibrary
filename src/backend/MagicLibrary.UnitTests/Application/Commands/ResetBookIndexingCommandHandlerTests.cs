using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class ResetBookIndexingCommandHandlerTests
{
    [Fact]
    public async Task Handle_BookDoesNotExist_ReturnsNotFound()
    {
        var bookId = Guid.NewGuid();
        var readRepository = new Mock<IBookReadRepository>();
        var writeRepository = new Mock<IBookWriteRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(bookId, CancellationToken.None))
            .ReturnsAsync((Book?)null);
        var handler = new ResetBookIndexingCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new ResetBookIndexingCommand(bookId),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(BookErrors.BookNotFound(bookId).Code, error.Code);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Book>(),
                CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCommand_ResetsAndPersistsBook()
    {
        var book = Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        book.MarkIndexingStarted();
        book.MarkIndexed(new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero));
        book.AddKeywords(["semantic", "search"]);
        var readRepository = new Mock<IBookReadRepository>();
        var writeRepository = new Mock<IBookWriteRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        writeRepository
            .Setup(repository => repository.UpdateAsync(book, CancellationToken.None))
            .Returns((Book updatedBook, CancellationToken _) => Task.FromResult(updatedBook));
        var handler = new ResetBookIndexingCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new ResetBookIndexingCommand(book.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(BookIndexingStatus.Pending, result.Value.IndexingStatus);
        Assert.Empty(result.Value.Keywords);
        Assert.Null(result.Value.IndexedAt);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<Book>(updatedBook =>
                    updatedBook.Id == book.Id
                    && updatedBook.IndexingStatus == BookIndexingStatus.Pending
                    && !updatedBook.Keywords.Any()
                    && updatedBook.IndexedAt == null),
                CancellationToken.None),
            Times.Once);
    }
}
