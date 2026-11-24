using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Indexing;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class RunIndexingJobCommandHandlerTests
{
    private readonly Mock<IBookReadRepository> _bookReadRepository = new();
    private readonly Mock<IIndexingJobWriteRepository> _indexingJobWriteRepository = new();
    private readonly Mock<IIndexingJobBackgroundProcessor> _backgroundProcessor = new();

    private RunIndexingJobCommandHandler CreateHandler() =>
        new(
            _bookReadRepository.Object,
            _indexingJobWriteRepository.Object,
            _backgroundProcessor.Object);

    [Fact]
    public async Task Handle_NullBookId_CreatesAndStartsLibraryWideJob()
    {
        _indexingJobWriteRepository
            .Setup(repository => repository.CreateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None))
            .Returns((IndexingJob job, CancellationToken _) => Task.FromResult(job));
        var handler = CreateHandler();

        var result = await handler.Handle(
            new RunIndexingCommand(null),
            CancellationToken.None);

        Assert.False(result.IsError);
        _bookReadRepository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<Guid>(), CancellationToken.None),
            Times.Never);
        _indexingJobWriteRepository.Verify(
            repository => repository.CreateAsync(
                It.Is<IndexingJob>(job =>
                    job.BookId == null
                    && job.Status == IndexingJobStatus.Pending),
                CancellationToken.None),
            Times.Once);
        _backgroundProcessor.Verify(
            processor => processor.Start(result.Value),
            Times.Once);
    }

    [Fact]
    public async Task Handle_BookId_CreatesAndStartsBookScopedJob()
    {
        var book = Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        _bookReadRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        _indexingJobWriteRepository
            .Setup(repository => repository.CreateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None))
            .Returns((IndexingJob job, CancellationToken _) => Task.FromResult(job));
        var handler = CreateHandler();

        var result = await handler.Handle(
            new RunIndexingCommand(book.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        _bookReadRepository.Verify(
            repository => repository.GetByIdAsync(book.Id, CancellationToken.None),
            Times.Once);
        _indexingJobWriteRepository.Verify(
            repository => repository.CreateAsync(
                It.Is<IndexingJob>(job =>
                    job.BookId == book.Id
                    && job.Status == IndexingJobStatus.Pending),
                CancellationToken.None),
            Times.Once);
        _backgroundProcessor.Verify(
            processor => processor.Start(result.Value),
            Times.Once);
    }

    [Fact]
    public async Task Handle_BookDoesNotExist_ReturnsNotFoundWithoutCreatingJob()
    {
        var bookId = Guid.NewGuid();
        _bookReadRepository
            .Setup(repository => repository.GetByIdAsync(bookId, CancellationToken.None))
            .ReturnsAsync((Book?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new RunIndexingCommand(bookId),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(BookErrors.BookNotFound(bookId).Code, error.Code);
        _indexingJobWriteRepository.Verify(
            repository => repository.CreateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None),
            Times.Never);
        _backgroundProcessor.Verify(
            processor => processor.Start(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CreateAsyncThrows_DoesNotStartProcessing()
    {
        _indexingJobWriteRepository
            .Setup(repository => repository.CreateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Persistence failed."));
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                new RunIndexingCommand(null),
                CancellationToken.None));

        _backgroundProcessor.Verify(
            processor => processor.Start(It.IsAny<Guid>()),
            Times.Never);
    }
}
