using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class MarkIndexingStartedCommandHandlerTests
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
        var handler = new MarkIndexingStartedCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new MarkIndexingStartedCommand(bookId),
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
        book.MarkIndexingStarted();
        book.MarkIndexed(new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero));
        var readRepository = new Mock<IBookReadRepository>();
        var writeRepository = new Mock<IBookWriteRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        var handler = new MarkIndexingStartedCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new MarkIndexingStartedCommand(book.Id),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(
            BookErrors.InvalidIndexingTransition(
                BookIndexingStatus.Indexed,
                BookIndexingStatus.InProgress).Code,
            error.Code);
        Assert.Contains(BookIndexingStatus.Indexed.ToString(), error.Description);
        Assert.Contains(BookIndexingStatus.InProgress.ToString(), error.Description);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Book>(),
                CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidTransition_MarksIndexingStartedAndPersists()
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
        writeRepository
            .Setup(repository => repository.UpdateAsync(book, CancellationToken.None))
            .Returns((Book updatedBook, CancellationToken _) => Task.FromResult(updatedBook));
        var handler = new MarkIndexingStartedCommandHandler(
            readRepository.Object,
            writeRepository.Object);

        var result = await handler.Handle(
            new MarkIndexingStartedCommand(book.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(BookIndexingStatus.InProgress, result.Value.IndexingStatus);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<Book>(updatedBook =>
                    updatedBook.Id == book.Id
                    && updatedBook.IndexingStatus == BookIndexingStatus.InProgress),
                CancellationToken.None),
            Times.Once);
    }
}
