using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Queries;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Queries;

public sealed class GetIndexingJobByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_LibraryWideJob_ReturnsNullBookId()
    {
        var job = IndexingJob.Create(null);
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(job.Id, CancellationToken.None))
            .ReturnsAsync(job);
        var handler = new GetIndexingJobByIdQueryHandler(indexingJobReadRepository.Object);

        var result = await handler.Handle(
            new GetIndexingJobByIdQuery(job.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(job.Id, result.Value.Id);
        Assert.Null(result.Value.BookId);
    }

    [Fact]
    public async Task Handle_ExistingJob_ReturnsCompleteJobState()
    {
        var bookId = Guid.NewGuid();
        var job = IndexingJob.Create(bookId);
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var completedAt = startedAt.AddMinutes(2);
        const string failureMessage = "Indexing failed.";
        const string progress = "Indexing page 1 of 3";
        job.Start(startedAt);
        job.UpdateIndexingProgress(progress);
        job.Fail(failureMessage, completedAt);
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(job.Id, CancellationToken.None))
            .ReturnsAsync(job);
        var handler = new GetIndexingJobByIdQueryHandler(indexingJobReadRepository.Object);

        var result = await handler.Handle(
            new GetIndexingJobByIdQuery(job.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(job.Id, result.Value.Id);
        Assert.Equal(bookId, result.Value.BookId);
        Assert.Equal(IndexingJobStatus.Failed, result.Value.Status);
        Assert.Equal(progress, result.Value.Progress);
        Assert.Equal(startedAt, result.Value.StartedAt);
        Assert.Equal(completedAt, result.Value.CompletedAt);
        Assert.Equal(failureMessage, result.Value.FailureMessage);
    }

    [Fact]
    public async Task Handle_JobDoesNotExist_ReturnsNotFound()
    {
        var jobId = Guid.NewGuid();
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(jobId, CancellationToken.None))
            .ReturnsAsync((IndexingJob?)null);
        var handler = new GetIndexingJobByIdQueryHandler(indexingJobReadRepository.Object);

        var result = await handler.Handle(
            new GetIndexingJobByIdQuery(jobId),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(IndexingJobErrors.IndexingJobNotFound(jobId).Code, error.Code);
    }
}
