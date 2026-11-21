using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Infrastructure.StorageModels.Mappers;

internal static class IndexingJobDocumentMapper
{
    internal static IndexingJobDocument ToDocument(IndexingJob job)
    {
        return new IndexingJobDocument
        {
            Id = job.Id,
            BookId = job.BookId,
            Status = job.Status,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            FailureMessage = job.FailureMessage
        };
    }

    internal static IndexingJob ToAggregate(IndexingJobDocument document)
    {
        return IndexingJob.Reconstitute(
            document.Id,
            document.BookId,
            document.Status,
            document.StartedAt,
            document.CompletedAt,
            document.FailureMessage);
    }
}
