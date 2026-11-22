using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validators;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class CompleteIndexingJobCommandHandlerTests
{
    [Fact]
    public async Task Handle_RunningJob_CompletesAndPersistsWithProgress()
    {
        var job = IndexingJob.Create(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var completedAt = startedAt.AddMinutes(2);
        const string progress = "Indexing page 1 of 3";
        job.Start(startedAt);
        job.UpdateIndexingProgress(progress);
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        var indexingJobWriteRepository = new Mock<IIndexingJobWriteRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(job.Id, CancellationToken.None))
            .ReturnsAsync(job);
        indexingJobWriteRepository
            .Setup(repository => repository.UpdateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None))
            .Returns((IndexingJob updatedJob, CancellationToken _) => Task.FromResult(updatedJob));
        var handler = new CompleteIndexingJobCommandHandler(
            new CompleteIndexingJobCommandValidator(),
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new CompleteIndexingJobCommand(job.Id, completedAt),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(IndexingJobStatus.Completed, result.Value.Status);
        Assert.Equal(startedAt, result.Value.StartedAt);
        Assert.Equal(completedAt, result.Value.CompletedAt);
        Assert.Equal(progress, result.Value.Progress);
        indexingJobWriteRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<IndexingJob>(updatedJob =>
                    updatedJob.Id == job.Id
                    && updatedJob.Status == IndexingJobStatus.Completed
                    && updatedJob.CompletedAt == completedAt
                    && updatedJob.Progress == progress),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_JobDoesNotExist_ReturnsNotFoundWithoutUpdatingJob()
    {
        var jobId = Guid.NewGuid();
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        var indexingJobWriteRepository = new Mock<IIndexingJobWriteRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(jobId, CancellationToken.None))
            .ReturnsAsync((IndexingJob?)null);
        var handler = new CompleteIndexingJobCommandHandler(
            new CompleteIndexingJobCommandValidator(),
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new CompleteIndexingJobCommand(
                jobId,
                new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero)),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(IndexingJobErrors.IndexingJobNotFound(jobId).Code, error.Code);
        indexingJobWriteRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None),
            Times.Never);
    }

    [Theory]
    [InlineData(IndexingJobStatus.Pending)]
    [InlineData(IndexingJobStatus.Completed)]
    [InlineData(IndexingJobStatus.Failed)]
    public async Task Handle_NonRunningJob_ReturnsInvalidTransitionWithoutUpdatingJob(
        IndexingJobStatus status)
    {
        var job = IndexingJob.Create(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var completedAt = startedAt.AddMinutes(2);
        if (status != IndexingJobStatus.Pending)
        {
            job.Start(startedAt);
        }

        if (status == IndexingJobStatus.Completed)
        {
            job.Complete(completedAt);
        }
        else if (status == IndexingJobStatus.Failed)
        {
            job.Fail("Indexing failed.", completedAt);
        }

        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        var indexingJobWriteRepository = new Mock<IIndexingJobWriteRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(job.Id, CancellationToken.None))
            .ReturnsAsync(job);
        var handler = new CompleteIndexingJobCommandHandler(
            new CompleteIndexingJobCommandValidator(),
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new CompleteIndexingJobCommand(job.Id, completedAt.AddMinutes(1)),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(
            IndexingJobErrors.InvalidIndexingTransition(
                status,
                IndexingJobStatus.Completed).Code,
            error.Code);
        Assert.Equal(status, job.Status);
        Assert.Equal(status == IndexingJobStatus.Pending ? null : startedAt, job.StartedAt);
        Assert.Equal(status == IndexingJobStatus.Pending ? null : completedAt, job.CompletedAt);
        indexingJobWriteRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DefaultCompletionTimestamp_ReturnsValidationErrorBeforeRepositoryAccess()
    {
        var jobId = Guid.NewGuid();
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        var indexingJobWriteRepository = new Mock<IIndexingJobWriteRepository>();
        var handler = new CompleteIndexingJobCommandHandler(
            new CompleteIndexingJobCommandValidator(),
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new CompleteIndexingJobCommand(jobId, default),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(IndexingJobErrors.InvalidCompletionTimestamp.Code, error.Code);
        indexingJobReadRepository.Verify(
            repository => repository.GetByIdAsync(jobId, CancellationToken.None),
            Times.Never);
        indexingJobWriteRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None),
            Times.Never);
    }
}
