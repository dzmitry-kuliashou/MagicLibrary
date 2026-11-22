using ErrorOr;
using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Application.Errors;

public static class IndexingJobErrors
{
    public static Error IndexingJobNotFound(Guid indexingJobId) =>
        Error.NotFound(
            "IndexingJob.NotFound",
            $"The indexing job with ID '{indexingJobId}' was not found.");

    public static Error InvalidIndexingTransition(
        IndexingJobStatus currentStatus,
        IndexingJobStatus targetStatus) =>
        Error.Conflict(
            "IndexingJob.InvalidIndexingTransition",
            $"An indexing job cannot transition from {currentStatus} to {targetStatus}.");

    public static Error ProgressUpdateNotAllowed(IndexingJobStatus currentStatus) =>
        Error.Conflict(
            "IndexingJob.ProgressUpdateNotAllowed",
            $"Progress can only be updated while an indexing job is Running; current status is {currentStatus}.");

    public static Error InvalidStartTimestamp =>
        Error.Validation(
            "IndexingJob.InvalidStartTimestamp",
            "A non-default start timestamp is required.");

    public static Error InvalidCompletionTimestamp =>
        Error.Validation(
            "IndexingJob.InvalidCompletionTimestamp",
            "A non-default completion timestamp is required.");

    public static Error MissingFailureMessage =>
        Error.Validation(
            "IndexingJob.MissingFailureMessage",
            "A failure message is required.");
}
