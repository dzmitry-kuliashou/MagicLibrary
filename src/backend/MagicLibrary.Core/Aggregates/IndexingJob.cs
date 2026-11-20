using Ardalis.GuardClauses;

namespace MagicLibrary.Core.Aggregates;

public enum IndexingJobStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public sealed class IndexingJob : AggregateRoot
{
    private IndexingJob(Guid bookId)
    {
        BookId = bookId;
        Status = IndexingJobStatus.Pending;
    }

    public Guid BookId { get; }

    public IndexingJobStatus Status { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureMessage { get; private set; }

    public static IndexingJob Create(Guid bookId)
    {
        Guard.Against.Default(bookId, nameof(bookId));
        return new IndexingJob(bookId);
    }

    public void Start(DateTimeOffset startedAt)
    {
        Guard.Against.Default(startedAt, nameof(startedAt));
        StartedAt = startedAt;
        Status = IndexingJobStatus.Running;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        Guard.Against.Default(completedAt, nameof(completedAt));
        CompletedAt = completedAt;
        Status = IndexingJobStatus.Completed;
    }

    public void Fail(string failureMessage, DateTimeOffset completedAt)
    {
        Guard.Against.NullOrWhiteSpace(failureMessage, nameof(failureMessage));
        Guard.Against.Default(completedAt, nameof(completedAt));
        CompletedAt = completedAt;
        FailureMessage = failureMessage;
        Status = IndexingJobStatus.Failed;
    }
}
