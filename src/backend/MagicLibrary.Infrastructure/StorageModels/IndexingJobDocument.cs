using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Infrastructure.StorageModels;

public sealed class IndexingJobDocument : StorageDocument
{
    public Guid BookId { get; set; }

    public IndexingJobStatus Status { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? FailureMessage { get; set; }
}
