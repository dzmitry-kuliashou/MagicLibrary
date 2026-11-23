using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class AddIndexingJobFileErrorCommandHandlerTests
{
    [Fact]
    public async Task Handle_RunningJob_AppendsAndPersistsFileError()
    {
        var job = IndexingJob.Create(Guid.NewGuid());
        job.Start(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
        const string fileName = "broken.pdf";
        const string message = "PDF extraction failed.";
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
        var handler = new AddIndexingJobFileErrorCommandHandler(
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new AddIndexingJobFileErrorCommand(job.Id, fileName, message),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Collection(
            result.Value.FileErrors,
            error =>
            {
                Assert.Equal(fileName, error.FileName);
                Assert.Equal(message, error.Message);
            });
        indexingJobWriteRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<IndexingJob>(updatedJob =>
                    updatedJob.Id == job.Id
                    && updatedJob.Status == IndexingJobStatus.Running
                    && updatedJob.FileErrors.Count == 1
                    && updatedJob.FileErrors[0].FileName == fileName
                    && updatedJob.FileErrors[0].Message == message),
                CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [InlineData(IndexingJobStatus.Pending)]
    [InlineData(IndexingJobStatus.Completed)]
    [InlineData(IndexingJobStatus.Failed)]
    public async Task Handle_NonRunningJob_ReturnsFileErrorUpdateNotAllowedWithoutUpdatingJob(
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
            job.Fail("Previous failure.", completedAt);
        }

        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        var indexingJobWriteRepository = new Mock<IIndexingJobWriteRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(job.Id, CancellationToken.None))
            .ReturnsAsync(job);
        var handler = new AddIndexingJobFileErrorCommandHandler(
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new AddIndexingJobFileErrorCommand(job.Id, "broken.pdf", "PDF extraction failed."),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(IndexingJobErrors.FileErrorUpdateNotAllowed(status).Code, error.Code);
        Assert.Equal(status, job.Status);
        Assert.Empty(job.FileErrors);
        indexingJobWriteRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<IndexingJob>(),
                CancellationToken.None),
            Times.Never);
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
        var handler = new AddIndexingJobFileErrorCommandHandler(
            indexingJobReadRepository.Object,
            indexingJobWriteRepository.Object);

        var result = await handler.Handle(
            new AddIndexingJobFileErrorCommand(jobId, "broken.pdf", "PDF extraction failed."),
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
}
