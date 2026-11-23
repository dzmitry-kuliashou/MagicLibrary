using Ardalis.GuardClauses;

namespace MagicLibrary.Core.Aggregates;

public sealed record IndexingJobFileError(string FileName, string Message);

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
        (IndexingJobStatus.Pending, IndexingJobStatus.Failed),
        (IndexingJobStatus.Running, IndexingJobStatus.Completed),
        (IndexingJobStatus.Running, IndexingJobStatus.Failed)
    ];

    private readonly List<IndexingJobFileError> _fileErrors = [];

    private IndexingJob(Guid? bookId)
    {
        BookId = bookId;
        Status = IndexingJobStatus.Pending;
        Progress = string.Empty;
    }

    private IndexingJob(
        Guid id,
        Guid? bookId,
        IndexingJobStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        string? failureMessage,
        string? progress,
        IEnumerable<IndexingJobFileError>? fileErrors)
        : base(id)
    {
        BookId = bookId;
        Status = status;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        FailureMessage = failureMessage;
        Progress = progress ?? string.Empty;
        if (fileErrors is not null)
        {
            _fileErrors.AddRange(fileErrors);
        }
    }

    public Guid? BookId { get; }

    public IndexingJobStatus Status { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureMessage { get; private set; }

    public string Progress { get; private set; }

    public IReadOnlyList<IndexingJobFileError> FileErrors =>
        Array.AsReadOnly(_fileErrors.ToArray());

    public bool CanTransitionTo(IndexingJobStatus status) =>
        AllowedTransitions.Contains((Status, status));

    public static IndexingJob Create(Guid? bookId)
    {
        if (bookId is { } id)
        {
            Guard.Against.Default(id);
        }

        return new IndexingJob(bookId);
    }

    internal static IndexingJob Reconstitute(
        Guid id,
        Guid? bookId,
        IndexingJobStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        string? failureMessage,
        string? progress = null,
        IEnumerable<IndexingJobFileError>? fileErrors = null)
    {
        // Persisted state is trusted; write-side APIs validate new aggregates.
        return new IndexingJob(
            id,
            bookId,
            status,
            startedAt,
            completedAt,
            failureMessage,
            progress,
            fileErrors);
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

    public void AddFileError(string fileName, string message)
    {
        Guard.Against.NullOrWhiteSpace(fileName);
        Guard.Against.NullOrWhiteSpace(message);
        Guard.Against.InvalidInput(
            Status,
            nameof(Status),
            status => status == IndexingJobStatus.Running,
            $"Cannot add an indexing job file error while status is {Status}.");

        _fileErrors.Add(new IndexingJobFileError(fileName, message));
    }
}
