using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class UpdateIndexingJobProgressCommandHandlerTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Indexing page 1 of 3")]
    public async Task Handle_RunningJob_StoresExactProgress(string progress)
    {
        var job = IndexingJob.Create(Guid.NewGuid());
        job.Start(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
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
        var handler = new UpdateIndexingJobProgressCommandHandler(
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new UpdateIndexingJobProgressCommand(job.Id, progress),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(IndexingJobStatus.Running, result.Value.Status);
        Assert.Equal(progress, result.Value.Progress);
        indexingJobWriteRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<IndexingJob>(updatedJob =>
                    updatedJob.Id == job.Id
                    && updatedJob.Status == IndexingJobStatus.Running
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
        var handler = new UpdateIndexingJobProgressCommandHandler(
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new UpdateIndexingJobProgressCommand(jobId, "Indexing page 1 of 3"),
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
    public async Task Handle_NonRunningJob_ReturnsProgressUpdateNotAllowedWithoutUpdatingJob(
        IndexingJobStatus status)
    {
        var job = IndexingJob.Create(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var completedAt = startedAt.AddMinutes(2);
        if (status != IndexingJobStatus.Pending)
        {
            job.Start(startedAt);
            job.UpdateIndexingProgress("Current progress");
        }

        if (status == IndexingJobStatus.Completed)
        {
            job.Complete(completedAt);
        }
        else if (status == IndexingJobStatus.Failed)
        {
            job.Fail("Previous failure.", completedAt);
        }

        var originalProgress = job.Progress;
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        var indexingJobWriteRepository = new Mock<IIndexingJobWriteRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(job.Id, CancellationToken.None))
            .ReturnsAsync(job);
        var handler = new UpdateIndexingJobProgressCommandHandler(
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new UpdateIndexingJobProgressCommand(job.Id, "New progress"),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(IndexingJobErrors.ProgressUpdateNotAllowed(status).Code, error.Code);
        Assert.Equal(status, job.Status);
        Assert.Equal(originalProgress, job.Progress);
        indexingJobWriteRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None),
            Times.Never);
    }
}
