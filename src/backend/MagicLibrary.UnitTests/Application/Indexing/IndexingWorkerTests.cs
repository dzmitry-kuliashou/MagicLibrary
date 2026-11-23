using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Indexing;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Indexing;

public sealed class IndexingWorkerTests
{
    public static TheoryData<IndexingJob?, string> InvalidJobs =>
        new()
        {
            { null, "was not found" },
            { CreateRunningJob(), "cannot transition" }
        };

    [Theory]
    [MemberData(nameof(InvalidJobs))]
    public async Task ProcessAsync_MissingOrNonPendingJob_ThrowsAndDoesNotStart(
        IndexingJob? job,
        string expectedMessage)
    {
        var indexingJobId = Guid.NewGuid();
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(
                indexingJobId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        var sender = new Mock<ISender>();
        var worker = new IndexingWorker(
            indexingJobReadRepository.Object,
            new IndexingScenarioFactory([]),
            TimeProvider.System,
            sender.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => worker.ProcessAsync(indexingJobId, CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        indexingJobReadRepository.Verify(
            repository => repository.AnyRunningAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_AnotherJobIsRunning_FailsPendingJobWithoutRunningScenario()
    {
        var indexingJob = IndexingJob.Create(null);
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(
                indexingJob.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexingJob);
        indexingJobReadRepository
            .Setup(repository => repository.AnyRunningAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var scenario = new Mock<IIndexingScenario>();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.IsAny<FailIndexingJobCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FailIndexingJobCommand command, CancellationToken _) =>
            {
                indexingJob.Fail(command.FailureMessage, command.CompletedAt);
                return indexingJob;
            });
        var worker = new IndexingWorker(
            indexingJobReadRepository.Object,
            new IndexingScenarioFactory([scenario.Object]),
            TimeProvider.System,
            sender.Object);

        await worker.ProcessAsync(indexingJob.Id, CancellationToken.None);

        Assert.Equal(IndexingJobStatus.Failed, indexingJob.Status);
        Assert.Equal(IndexingWorker.AnotherJobRunningMessage, indexingJob.FailureMessage);
        sender.Verify(
            service => service.Send(
                It.Is<FailIndexingJobCommand>(command =>
                    command.IndexingJobId == indexingJob.Id
                    && command.FailureMessage == IndexingWorker.AnotherJobRunningMessage),
                CancellationToken.None),
            Times.Once);
        sender.Verify(
            service => service.Send(
                It.IsAny<StartIndexingJobCommand>(),
                CancellationToken.None),
            Times.Never);
        scenario.Verify(
            candidate => candidate.CanProcess(It.IsAny<IndexingJob>()),
            Times.Never);
        scenario.Verify(
            candidate => candidate.ExecuteAsync(
                It.IsAny<IndexingJob>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_SuccessfulLibraryScenario_CompletesJob()
    {
        var indexingJob = IndexingJob.Create(null);
        var timestamp = new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(timestamp);
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(
                indexingJob.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexingJob);
        indexingJobReadRepository
            .Setup(repository => repository.AnyRunningAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var scenario = new Mock<IIndexingScenario>();
        scenario
            .Setup(candidate => candidate.CanProcess(indexingJob))
            .Returns(true);
        scenario
            .Setup(candidate => candidate.ExecuteAsync(indexingJob, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ErrorOr<Success>>(Result.Success));
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.IsAny<StartIndexingJobCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((StartIndexingJobCommand command, CancellationToken _) =>
            {
                indexingJob.Start(command.StartedAt);
                return Task.FromResult<ErrorOr<IndexingJob>>(indexingJob);
            });
        sender
            .Setup(service => service.Send(
                It.IsAny<CompleteIndexingJobCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((CompleteIndexingJobCommand command, CancellationToken _) =>
            {
                indexingJob.Complete(command.CompletedAt);
                return Task.FromResult<ErrorOr<IndexingJob>>(indexingJob);
            });
        var worker = new IndexingWorker(
            indexingJobReadRepository.Object,
            new IndexingScenarioFactory([scenario.Object]),
            timeProvider,
            sender.Object);

        await worker.ProcessAsync(indexingJob.Id, CancellationToken.None);

        Assert.Equal(IndexingJobStatus.Completed, indexingJob.Status);
        Assert.Equal(timestamp, indexingJob.StartedAt);
        Assert.Equal(timestamp, indexingJob.CompletedAt);
        sender.Verify(
            service => service.Send(
                It.Is<StartIndexingJobCommand>(command =>
                    command.IndexingJobId == indexingJob.Id
                    && command.StartedAt == timestamp),
                CancellationToken.None),
            Times.Once);
        scenario.Verify(
            candidate => candidate.ExecuteAsync(indexingJob, CancellationToken.None),
            Times.Once);
        sender.Verify(
            service => service.Send(
                It.Is<CompleteIndexingJobCommand>(command =>
                    command.IndexingJobId == indexingJob.Id
                    && command.CompletedAt == timestamp),
                CancellationToken.None),
            Times.Once);
        sender.Verify(
            service => service.Send(
                It.IsAny<FailIndexingJobCommand>(),
                CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_BookSpecificScenarioFails_FailsJobWithScenarioMessage()
    {
        var indexingJob = IndexingJob.Create(Guid.NewGuid());
        var timestamp = new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(timestamp);
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(
                indexingJob.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexingJob);
        indexingJobReadRepository
            .Setup(repository => repository.AnyRunningAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.IsAny<StartIndexingJobCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((StartIndexingJobCommand command, CancellationToken _) =>
            {
                indexingJob.Start(command.StartedAt);
                return Task.FromResult<ErrorOr<IndexingJob>>(indexingJob);
            });
        sender
            .Setup(service => service.Send(
                It.IsAny<FailIndexingJobCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((FailIndexingJobCommand command, CancellationToken _) =>
            {
                indexingJob.Fail(command.FailureMessage, command.CompletedAt);
                return Task.FromResult<ErrorOr<IndexingJob>>(indexingJob);
            });
        var worker = new IndexingWorker(
            indexingJobReadRepository.Object,
            new IndexingScenarioFactory([new BookIndexingScenario()]),
            timeProvider,
            sender.Object);

        await worker.ProcessAsync(indexingJob.Id, CancellationToken.None);

        Assert.Equal(IndexingJobStatus.Failed, indexingJob.Status);
        Assert.Equal("Book-specific indexing is not implemented.", indexingJob.FailureMessage);
        Assert.Equal(timestamp, indexingJob.CompletedAt);
        sender.Verify(
            service => service.Send(
                It.Is<FailIndexingJobCommand>(command =>
                    command.IndexingJobId == indexingJob.Id
                    && command.FailureMessage == "Book-specific indexing is not implemented."),
                CancellationToken.None),
            Times.Once);
        sender.Verify(
            service => service.Send(
                It.IsAny<CompleteIndexingJobCommand>(),
                CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ScenarioThrows_FailsJobWithExceptionMessage()
    {
        var indexingJob = IndexingJob.Create(null);
        const string failureMessage = "The library scan failed unexpectedly.";
        var timestamp = new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(timestamp);
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        indexingJobReadRepository
            .Setup(repository => repository.GetByIdAsync(
                indexingJob.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexingJob);
        indexingJobReadRepository
            .Setup(repository => repository.AnyRunningAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var scenario = new Mock<IIndexingScenario>();
        scenario
            .Setup(candidate => candidate.CanProcess(indexingJob))
            .Returns(true);
        scenario
            .Setup(candidate => candidate.ExecuteAsync(indexingJob, It.IsAny<CancellationToken>()))
            .Returns(Task.FromException<ErrorOr<Success>>(
                new InvalidOperationException(failureMessage)));
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.IsAny<StartIndexingJobCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((StartIndexingJobCommand command, CancellationToken _) =>
            {
                indexingJob.Start(command.StartedAt);
                return Task.FromResult<ErrorOr<IndexingJob>>(indexingJob);
            });
        sender
            .Setup(service => service.Send(
                It.IsAny<FailIndexingJobCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((FailIndexingJobCommand command, CancellationToken _) =>
            {
                indexingJob.Fail(command.FailureMessage, command.CompletedAt);
                return Task.FromResult<ErrorOr<IndexingJob>>(indexingJob);
            });
        var worker = new IndexingWorker(
            indexingJobReadRepository.Object,
            new IndexingScenarioFactory([scenario.Object]),
            timeProvider,
            sender.Object);

        await worker.ProcessAsync(indexingJob.Id, CancellationToken.None);

        Assert.Equal(IndexingJobStatus.Failed, indexingJob.Status);
        Assert.Equal(failureMessage, indexingJob.FailureMessage);
        Assert.Equal(timestamp, indexingJob.CompletedAt);
        sender.Verify(
            service => service.Send(
                It.Is<FailIndexingJobCommand>(command =>
                    command.IndexingJobId == indexingJob.Id
                    && command.FailureMessage == failureMessage),
                CancellationToken.None),
            Times.Once);
        sender.Verify(
            service => service.Send(
                It.IsAny<CompleteIndexingJobCommand>(),
                CancellationToken.None),
            Times.Never);
    }

    private static IndexingJob CreateRunningJob()
    {
        var indexingJob = IndexingJob.Create(null);
        indexingJob.Start(DateTimeOffset.UtcNow);
        return indexingJob;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
