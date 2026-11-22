using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class MarkBookIndexingFailedCommandHandlerTests
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
        var handler = new MarkBookIndexingFailedCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new MarkBookIndexingFailedCommand(bookId),
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
    public async Task Handle_InvalidTransition_ReturnsConflict()
    {
        var book = Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var readRepository = new Mock<IBookReadRepository>();
        var writeRepository = new Mock<IBookWriteRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        var handler = new MarkBookIndexingFailedCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new MarkBookIndexingFailedCommand(book.Id),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(
            BookErrors.InvalidIndexingTransition(
                BookIndexingStatus.Pending,
                BookIndexingStatus.Failed).Code,
            error.Code);
        Assert.Contains(BookIndexingStatus.Pending.ToString(), error.Description);
        Assert.Contains(BookIndexingStatus.Failed.ToString(), error.Description);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Book>(),
                CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidTransition_MarksIndexingFailedAndPersists()
    {
        var book = Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        book.MarkIndexingStarted();
        var readRepository = new Mock<IBookReadRepository>();
        var writeRepository = new Mock<IBookWriteRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        writeRepository
            .Setup(repository => repository.UpdateAsync(book, CancellationToken.None))
            .Returns((Book updatedBook, CancellationToken _) => Task.FromResult(updatedBook));
        var handler = new MarkBookIndexingFailedCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new MarkBookIndexingFailedCommand(book.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(BookIndexingStatus.Failed, result.Value.IndexingStatus);
        Assert.Null(result.Value.IndexedAt);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<Book>(updatedBook =>
                    updatedBook.Id == book.Id
                    && updatedBook.IndexingStatus == BookIndexingStatus.Failed
                    && updatedBook.IndexedAt == null),
                CancellationToken.None),
            Times.Once);
    }
}
