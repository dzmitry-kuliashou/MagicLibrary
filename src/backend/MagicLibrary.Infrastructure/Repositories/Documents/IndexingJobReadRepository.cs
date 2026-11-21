using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories.Documents;

internal sealed class IndexingJobReadRepository(IQuerySession querySession)
    : DocumentReadRepository<IndexingJob, IndexingJobDocument>(querySession), IIndexingJobReadRepository
{
    protected override IndexingJob ToAggregate(IndexingJobDocument document) =>
        IndexingJobDocumentMapper.ToAggregate(document);
}
