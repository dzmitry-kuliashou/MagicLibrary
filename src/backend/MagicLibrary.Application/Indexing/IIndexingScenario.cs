using ErrorOr;
using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Application.Indexing;

public interface IIndexingScenario
{
    bool CanProcess(IndexingJob indexingJob);

    Task<ErrorOr<Success>> ExecuteAsync(
        IndexingJob indexingJob,
        CancellationToken cancellationToken);
}
