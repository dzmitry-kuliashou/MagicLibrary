namespace MagicLibrary.Contract.Responses;

public sealed record IndexingJobResponse(
    Guid Id,
    Guid? BookId,
    string Status,
    string Progress,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureMessage,
    IReadOnlyList<IndexingJobFileErrorResponse> FileErrors);

public sealed record IndexingJobFileErrorResponse(string FileName, string Message);
