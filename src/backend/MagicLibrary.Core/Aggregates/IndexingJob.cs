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

    private IndexingJob(
        Guid id,
        Guid bookId,
        IndexingJobStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        string? failureMessage)
        : base(id)
    {
        BookId = bookId;
        Status = status;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        FailureMessage = failureMessage;
    }

    public Guid BookId { get; }

    public IndexingJobStatus Status { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureMessage { get; private set; }

    public static IndexingJob Create(Guid bookId)
    {
        Guard.Against.Default(bookId);
        return new IndexingJob(bookId);
    }

    internal static IndexingJob Reconstitute(
        Guid id,
        Guid bookId,
        IndexingJobStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        string? failureMessage)
    {
        // Persisted state is trusted; write-side APIs validate new aggregates.
        return new IndexingJob(id, bookId, status, startedAt, completedAt, failureMessage);
    }

    public void Start(DateTimeOffset startedAt)
    {
        Guard.Against.Default(startedAt);
        StartedAt = startedAt;
        Status = IndexingJobStatus.Running;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        Guard.Against.Default(completedAt);
        CompletedAt = completedAt;
        Status = IndexingJobStatus.Completed;
    }

    public void Fail(string failureMessage, DateTimeOffset completedAt)
    {
        Guard.Against.NullOrWhiteSpace(failureMessage);
        Guard.Against.Default(completedAt);
        CompletedAt = completedAt;
        FailureMessage = failureMessage;
        Status = IndexingJobStatus.Failed;
    }
}
