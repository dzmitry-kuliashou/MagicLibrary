using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Indexing;

public sealed class IndexingWorker(
    IIndexingJobReadRepository indexingJobReadRepository,
    IndexingScenarioFactory indexingScenarioFactory,
    TimeProvider timeProvider,
    ISender sender)
{
    public const string AnotherJobRunningMessage = "Another indexing job is already running.";

    public async Task ProcessAsync(
        Guid indexingJobId,
        CancellationToken cancellationToken)
    {
        var indexingJob = await indexingJobReadRepository.GetByIdAsync(
            indexingJobId,
            cancellationToken);
        if (indexingJob is null)
        {
            throw new InvalidOperationException(
                IndexingJobErrors.IndexingJobNotFound(indexingJobId).Description);
        }

        if (indexingJob.Status != IndexingJobStatus.Pending)
        {
            throw new InvalidOperationException(
                IndexingJobErrors.InvalidIndexingTransition(
                    indexingJob.Status,
                    IndexingJobStatus.Running).Description);
        }

        if (await indexingJobReadRepository.AnyRunningAsync(cancellationToken))
        {
            await FailJobAsync(
                indexingJobId,
                AnotherJobRunningMessage,
                cancellationToken);
            return;
        }

        var startResult = await sender.Send(
            new StartIndexingJobCommand(indexingJobId, timeProvider.GetUtcNow()),
            cancellationToken);
        if (startResult.IsError)
        {
            await FailJobAsync(
                indexingJobId,
                GetFailureMessage(startResult.Errors),
                cancellationToken);
            return;
        }

        var startedJob = startResult.Value;
        ErrorOr<Success> scenarioResult;
        try
        {
            var scenario = indexingScenarioFactory.GetScenario(startedJob);
            scenarioResult = await scenario.ExecuteAsync(startedJob, cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            var failureMessage = string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message;
            await FailJobAsync(
                indexingJobId,
                failureMessage,
                cancellationToken);
            return;
        }

        if (scenarioResult.IsError)
        {
            var failureMessage = string.Join(
                "; ",
                scenarioResult.Errors.Select(error => error.Description));
            await FailJobAsync(
                indexingJobId,
                failureMessage,
                cancellationToken);
            return;
        }

        var completeResult = await sender.Send(
            new CompleteIndexingJobCommand(indexingJobId, timeProvider.GetUtcNow()),
            cancellationToken);
        if (completeResult.IsError)
        {
            await FailJobAsync(
                indexingJobId,
                GetFailureMessage(completeResult.Errors),
                cancellationToken);
        }
    }

    private async Task FailJobAsync(
        Guid indexingJobId,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var failureResult = await sender.Send(
            new FailIndexingJobCommand(indexingJobId, failureMessage, timeProvider.GetUtcNow()),
            cancellationToken);
        if (failureResult.IsError)
        {
            throw new InvalidOperationException(GetFailureMessage(failureResult.Errors));
        }
    }

    private static string GetFailureMessage(IEnumerable<Error> errors) =>
        string.Join("; ", errors.Select(error => error.Description));
}
