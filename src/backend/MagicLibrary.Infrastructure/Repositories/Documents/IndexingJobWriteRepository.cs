using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories.Documents;

internal sealed class IndexingJobWriteRepository(IDocumentSession documentSession)
    : DocumentWriteRepository<IndexingJob, IndexingJobDocument>(documentSession), IIndexingJobWriteRepository
{
    protected override IndexingJobDocument ToDocument(IndexingJob aggregate) =>
        IndexingJobDocumentMapper.ToDocument(aggregate);
}
