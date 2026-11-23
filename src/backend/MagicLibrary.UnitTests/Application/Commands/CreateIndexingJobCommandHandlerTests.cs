using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class CreateIndexingJobCommandHandlerTests
{
    [Fact]
    public async Task Handle_NoBookId_CreatesAndPersistsLibraryWideJob()
    {
        var bookReadRepository = new Mock<IBookReadRepository>();
        var indexingJobWriteRepository = new Mock<IIndexingJobWriteRepository>();
        indexingJobWriteRepository
            .Setup(repository => repository.CreateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None))
            .Returns((IndexingJob job, CancellationToken _) => Task.FromResult(job));
        var handler = new CreateIndexingJobCommandHandler(
            bookReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new CreateIndexingJobCommand(),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Null(result.Value.BookId);
        Assert.Equal(IndexingJobStatus.Pending, result.Value.Status);
        bookReadRepository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<Guid>(), CancellationToken.None),
            Times.Never);
        indexingJobWriteRepository.Verify(
            repository => repository.CreateAsync(
                It.Is<IndexingJob>(job =>
                    job.BookId == null
                    && job.Status == IndexingJobStatus.Pending),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingBook_CreatesAndPersistsIndexingJob()
    {
        var book = Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var bookReadRepository = new Mock<IBookReadRepository>();
        var indexingJobWriteRepository = new Mock<IIndexingJobWriteRepository>();
        bookReadRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        indexingJobWriteRepository
            .Setup(repository => repository.CreateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None))
            .Returns((IndexingJob job, CancellationToken _) => Task.FromResult(job));
        var handler = new CreateIndexingJobCommandHandler(
            bookReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new CreateIndexingJobCommand(book.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(book.Id, result.Value.BookId);
        Assert.Equal(IndexingJobStatus.Pending, result.Value.Status);
        indexingJobWriteRepository.Verify(
            repository => repository.CreateAsync(
                It.Is<IndexingJob>(job =>
                    job.BookId == book.Id
                    && job.Status == IndexingJobStatus.Pending),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_BookDoesNotExist_ReturnsNotFoundWithoutCreatingJob()
    {
        var bookId = Guid.NewGuid();
        var bookReadRepository = new Mock<IBookReadRepository>();
        var indexingJobWriteRepository = new Mock<IIndexingJobWriteRepository>();
        bookReadRepository
            .Setup(repository => repository.GetByIdAsync(bookId, CancellationToken.None))
            .ReturnsAsync((Book?)null);
        var handler = new CreateIndexingJobCommandHandler(
            bookReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new CreateIndexingJobCommand(bookId),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(BookErrors.BookNotFound(bookId).Code, error.Code);
        indexingJobWriteRepository.Verify(
            repository => repository.CreateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None),
            Times.Never);
    }
}
