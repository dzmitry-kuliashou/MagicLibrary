namespace MagicLibrary.Contract.IndexingJobs.Responses;

public sealed record IndexingJobResponse(
    Guid Id,
    Guid BookId,
    string Status,
    string Progress,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureMessage);
