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

    public static Error FileErrorUpdateNotAllowed(IndexingJobStatus currentStatus) =>
        Error.Conflict(
            "IndexingJob.FileErrorUpdateNotAllowed",
            $"A file error can only be added while an indexing job is Running; current status is {currentStatus}.");

    public static Error BookIdRequired =>
        Error.Validation(
            "IndexingJob.BookIdRequired",
            "A book ID is required for book indexing.");

    public static Error BookStorageRootDirectoryUnavailable =>
        Error.Failure(
            "BookStorage.RootDirectoryUnavailable",
            "The configured book storage directory does not exist.");

    public static Error BookPdfNotFound(string fileName) =>
        Error.NotFound(
            "IndexingJob.BookPdfNotFound",
            $"The PDF file '{fileName}' does not exist in the configured book storage directory.");

    public static Error BookPdfExtractionFailed(string fileName, Exception exception) =>
        Error.Failure(
            "IndexingJob.BookPdfExtractionFailed",
            $"Failed to extract '{fileName}': {exception.GetType().Name}: {exception.Message}");

    public static Error BookIndexingFailed(string fileName, string message) =>
        Error.Failure(
            "IndexingJob.BookIndexingFailed",
            $"Indexing book '{fileName}' failed: {message}");

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
