using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class MarkBookIndexedCommandHandlerTests
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
        var handler = new MarkBookIndexedCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new MarkBookIndexedCommand(
                bookId,
                new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero)),
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
        var handler = new MarkBookIndexedCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new MarkBookIndexedCommand(
                book.Id,
                new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero)),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(
            BookErrors.InvalidIndexingTransition(
                BookIndexingStatus.Pending,
                BookIndexingStatus.Indexed).Code,
            error.Code);
        Assert.Contains(BookIndexingStatus.Pending.ToString(), error.Description);
        Assert.Contains(BookIndexingStatus.Indexed.ToString(), error.Description);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Book>(),
                CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidTransition_UsesCommandTimestampAndPersists()
    {
        var book = Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        book.MarkIndexingStarted();
        var indexedAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.FromHours(-5));
        var readRepository = new Mock<IBookReadRepository>();
        var writeRepository = new Mock<IBookWriteRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        writeRepository
            .Setup(repository => repository.UpdateAsync(book, CancellationToken.None))
            .Returns((Book updatedBook, CancellationToken _) => Task.FromResult(updatedBook));
        var handler = new MarkBookIndexedCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new MarkBookIndexedCommand(book.Id, indexedAt),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(BookIndexingStatus.Indexed, result.Value.IndexingStatus);
        Assert.Equal(indexedAt, result.Value.IndexedAt);
        Assert.Equal(indexedAt.Offset, result.Value.IndexedAt!.Value.Offset);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<Book>(updatedBook =>
                    updatedBook.Id == book.Id
                    && updatedBook.IndexingStatus == BookIndexingStatus.Indexed
                    && updatedBook.IndexedAt == indexedAt
                    && updatedBook.IndexedAt.Value.Offset == indexedAt.Offset),
                CancellationToken.None),
            Times.Once);
    }
}
