using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Core.Repositories.Documents;

public interface IIndexingJobReadRepository : IDocumentReadRepository<IndexingJob>
{
    Task<bool> AnyRunningAsync(CancellationToken cancellationToken);
}
