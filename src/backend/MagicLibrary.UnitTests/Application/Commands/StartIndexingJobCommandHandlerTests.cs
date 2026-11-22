using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validators;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class StartIndexingJobCommandHandlerTests
{
    [Fact]
    public async Task Handle_PendingJob_StartsAndPersistsWithSuppliedTimestamp()
    {
        var job = IndexingJob.Create(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
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
        var handler = new StartIndexingJobCommandHandler(
            new StartIndexingJobCommandValidator(),
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new StartIndexingJobCommand(job.Id, startedAt),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(IndexingJobStatus.Running, result.Value.Status);
        Assert.Equal(startedAt, result.Value.StartedAt);
        indexingJobWriteRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<IndexingJob>(updatedJob =>
                    updatedJob.Id == job.Id
                    && updatedJob.Status == IndexingJobStatus.Running
                    && updatedJob.StartedAt == startedAt),
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
        var handler = new StartIndexingJobCommandHandler(
            new StartIndexingJobCommandValidator(),
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new StartIndexingJobCommand(
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
    [InlineData(IndexingJobStatus.Running)]
    [InlineData(IndexingJobStatus.Completed)]
    [InlineData(IndexingJobStatus.Failed)]
    public async Task Handle_NonPendingJob_ReturnsInvalidTransitionWithoutUpdatingJob(
        IndexingJobStatus status)
    {
        var job = IndexingJob.Create(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var completedAt = startedAt.AddMinutes(2);
        job.Start(startedAt);
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
        var handler = new StartIndexingJobCommandHandler(
            new StartIndexingJobCommandValidator(),
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new StartIndexingJobCommand(job.Id, completedAt.AddMinutes(1)),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(
            IndexingJobErrors.InvalidIndexingTransition(
                status,
                IndexingJobStatus.Running).Code,
            error.Code);
        Assert.Equal(status, job.Status);
        Assert.Equal(startedAt, job.StartedAt);
        Assert.Equal(status == IndexingJobStatus.Running ? null : completedAt, job.CompletedAt);
        indexingJobWriteRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DefaultStartTimestamp_ReturnsValidationErrorBeforeRepositoryAccess()
    {
        var jobId = Guid.NewGuid();
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        var indexingJobWriteRepository = new Mock<IIndexingJobWriteRepository>();
        var handler = new StartIndexingJobCommandHandler(
            new StartIndexingJobCommandValidator(),
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new StartIndexingJobCommand(jobId, default),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(IndexingJobErrors.InvalidStartTimestamp.Code, error.Code);
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
