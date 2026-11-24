using System.Collections.Concurrent;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Indexing;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Indexing;

public sealed class IndexingJobBackgroundProcessorTests
{
    [Fact]
    public async Task Start_ProcessesJobInNewScope()
    {
        var firstJob = IndexingJob.Create(null);
        var secondJob = IndexingJob.Create(null);
        var jobs = new[] { firstJob, secondJob };
        var indexingJobReadRepository = new Mock<IIndexingJobReadRepository>();
        foreach (var job in jobs)
        {
            indexingJobReadRepository
                .Setup(repository => repository.GetByIdAsync(
                    job.Id,
                    CancellationToken.None))
                .ReturnsAsync(job);
        }

        indexingJobReadRepository
            .Setup(repository => repository.AnyRunningAsync(CancellationToken.None))
            .ReturnsAsync(true);

        var processingCompleted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completedCount = 0;
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.IsAny<FailIndexingJobCommand>(),
                CancellationToken.None))
            .ReturnsAsync((FailIndexingJobCommand command, CancellationToken _) =>
            {
                var job = Assert.Single(jobs, candidate => candidate.Id == command.IndexingJobId);
                job.Fail(command.FailureMessage, command.CompletedAt);
                if (Interlocked.Increment(ref completedCount) == jobs.Length)
                {
                    processingCompleted.TrySetResult(true);
                }

                return job;
            });

        var scopedMarkers = new ConcurrentBag<ScopedMarker>();
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ScopedMarker>();
        services.AddScoped<IIndexingJobReadRepository>(serviceProvider =>
        {
            scopedMarkers.Add(serviceProvider.GetRequiredService<ScopedMarker>());
            return indexingJobReadRepository.Object;
        });
        services.AddScoped<IndexingScenarioFactory>();
        services.AddScoped<IndexingWorker>();
        services.AddScoped<ISender>(_ => sender.Object);
        await using var serviceProvider = services.BuildServiceProvider();
        var processor = new IndexingJobBackgroundProcessor(
            serviceProvider.GetRequiredService<IServiceScopeFactory>());

        processor.Start(firstJob.Id);
        processor.Start(secondJob.Id);
        await processingCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.All(jobs, job => Assert.Equal(IndexingJobStatus.Failed, job.Status));
        Assert.Equal(2, scopedMarkers.Distinct().Count());
        foreach (var job in jobs)
        {
            indexingJobReadRepository.Verify(
                repository => repository.GetByIdAsync(job.Id, CancellationToken.None),
                Times.Once);
        }
    }

    private sealed class ScopedMarker;
}
