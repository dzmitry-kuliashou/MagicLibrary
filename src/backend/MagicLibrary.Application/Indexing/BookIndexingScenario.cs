using ErrorOr;
using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Application.Indexing;

public sealed class BookIndexingScenario : IIndexingScenario
{
    public bool CanProcess(IndexingJob indexingJob) => indexingJob.BookId is not null;

    public Task<ErrorOr<Success>> ExecuteAsync(
        IndexingJob indexingJob,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(indexingJob);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<ErrorOr<Success>>(
            Error.Failure(
                "IndexingJob.BookIndexingNotImplemented",
                "Book-specific indexing is not implemented."));
    }
}
