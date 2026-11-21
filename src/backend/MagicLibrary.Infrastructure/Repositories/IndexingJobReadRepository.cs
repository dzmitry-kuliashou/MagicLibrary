using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories;
using MagicLibrary.Infrastructure.StorageModels;
using MagicLibrary.Infrastructure.StorageModels.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories;

internal sealed class IndexingJobReadRepository(IQuerySession querySession)
    : ReadRepository<IndexingJob, IndexingJobDocument>(querySession), IIndexingJobReadRepository
{
    protected override IndexingJob ToAggregate(IndexingJobDocument document) =>
        IndexingJobDocumentMapper.ToAggregate(document);
}
