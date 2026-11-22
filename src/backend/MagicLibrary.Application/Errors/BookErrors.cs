using ErrorOr;
using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Application.Errors;

public static class BookErrors
{
    public static Error MissingFileName =>
        Error.Validation("Book.MissingFileName", "A filename is required.");

    public static Error UnsafeFileName =>
        Error.Validation("Book.UnsafeFileName", "The filename must be a leaf filename without path separators.");

    public static Error MissingTitle =>
        Error.Validation("Book.MissingTitle", "A title is required.");

    public static Error MissingStoragePath =>
        Error.Failure("Book.MissingStoragePath", "The book storage root directory is not configured.");

    public static Error InvalidExtension =>
        Error.Validation("Book.InvalidExtension", "The source file must have a .pdf extension.");

    public static Error FileNotFound =>
        Error.NotFound("Book.FileNotFound", "The PDF file was not found.");

    public static Error DuplicateFileName =>
        Error.Conflict("Book.DuplicateFileName", "A book with this filename already exists.");

    public static Error BookNotFound(Guid bookId) =>
        Error.NotFound("Book.NotFound", $"The book with ID '{bookId}' was not found.");

    public static Error InvalidIndexingTransition(
        BookIndexingStatus currentStatus,
        BookIndexingStatus targetStatus) =>
        Error.Conflict(
            "Book.InvalidIndexingTransition",
            $"A book cannot transition from {currentStatus} to {targetStatus}.");
}
