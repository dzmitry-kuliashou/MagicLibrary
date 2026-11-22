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
    private static readonly HashSet<(IndexingJobStatus From, IndexingJobStatus To)> AllowedTransitions =
    [
        (IndexingJobStatus.Pending, IndexingJobStatus.Running),
        (IndexingJobStatus.Running, IndexingJobStatus.Completed),
        (IndexingJobStatus.Running, IndexingJobStatus.Failed)
    ];

    private IndexingJob(Guid bookId)
    {
        BookId = bookId;
        Status = IndexingJobStatus.Pending;
        Progress = string.Empty;
    }

    private IndexingJob(
        Guid id,
        Guid bookId,
        IndexingJobStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        string? failureMessage,
        string? progress)
        : base(id)
    {
        BookId = bookId;
        Status = status;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        FailureMessage = failureMessage;
        Progress = progress ?? string.Empty;
    }

    public Guid BookId { get; }

    public IndexingJobStatus Status { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureMessage { get; private set; }

    public string Progress { get; private set; }

    public bool CanTransitionTo(IndexingJobStatus status) =>
        AllowedTransitions.Contains((Status, status));

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
        string? failureMessage,
        string? progress = null)
    {
        // Persisted state is trusted; write-side APIs validate new aggregates.
        return new IndexingJob(id, bookId, status, startedAt, completedAt, failureMessage, progress);
    }

    public void Start(DateTimeOffset startedAt)
    {
        Guard.Against.InvalidInput(
            Status,
            nameof(Status),
            status => AllowedTransitions.Contains((status, IndexingJobStatus.Running)),
            $"Cannot transition indexing job status from {Status} to {IndexingJobStatus.Running}.");
        Guard.Against.Default(startedAt);
        StartedAt = startedAt;
        Status = IndexingJobStatus.Running;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        Guard.Against.InvalidInput(
            Status,
            nameof(Status),
            status => AllowedTransitions.Contains((status, IndexingJobStatus.Completed)),
            $"Cannot transition indexing job status from {Status} to {IndexingJobStatus.Completed}.");
        Guard.Against.Default(completedAt);
        CompletedAt = completedAt;
        Status = IndexingJobStatus.Completed;
    }

    public void Fail(string failureMessage, DateTimeOffset completedAt)
    {
        Guard.Against.InvalidInput(
            Status,
            nameof(Status),
            status => AllowedTransitions.Contains((status, IndexingJobStatus.Failed)),
            $"Cannot transition indexing job status from {Status} to {IndexingJobStatus.Failed}.");
        Guard.Against.NullOrWhiteSpace(failureMessage);
        Guard.Against.Default(completedAt);
        CompletedAt = completedAt;
        FailureMessage = failureMessage;
        Status = IndexingJobStatus.Failed;
    }

    public void UpdateIndexingProgress(string progress)
    {
        Guard.Against.Null(progress);
        Guard.Against.InvalidInput(
            Status,
            nameof(Status),
            status => status == IndexingJobStatus.Running,
            $"Cannot update indexing job progress while status is {Status}.");

        Progress = progress;
    }
}
