using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories;
using MagicLibrary.Infrastructure.StorageModels;
using MagicLibrary.Infrastructure.StorageModels.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories;

internal sealed class IndexingJobWriteRepository(IDocumentSession documentSession)
    : WriteRepository<IndexingJob, IndexingJobDocument>(documentSession), IIndexingJobWriteRepository
{
    protected override IndexingJobDocument ToDocument(IndexingJob aggregate) =>
        IndexingJobDocumentMapper.ToDocument(aggregate);
}
