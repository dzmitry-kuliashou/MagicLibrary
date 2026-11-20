using Ardalis.GuardClauses;

namespace MagicLibrary.Core.ValueObjects;

public sealed record SourceReference
{
    private SourceReference(Guid bookId, string bookTitle, int pageNumber, Guid? chunkId)
    {
        BookId = bookId;
        BookTitle = bookTitle;
        PageNumber = pageNumber;
        ChunkId = chunkId;
    }

    public Guid BookId { get; }

    public string BookTitle { get; }

    public int PageNumber { get; }

    public Guid? ChunkId { get; }

    public static SourceReference Create(Guid bookId, string bookTitle, int pageNumber, Guid? chunkId = null)
    {
        Guard.Against.Default(bookId, nameof(bookId));
        Guard.Against.NullOrWhiteSpace(bookTitle, nameof(bookTitle));
        Guard.Against.NegativeOrZero(pageNumber, nameof(pageNumber));
        Guard.Against.InvalidInput(
            chunkId,
            nameof(chunkId),
            id => id is null || id != Guid.Empty,
            "ChunkId must be a non-empty GUID when provided.");

        return new SourceReference(bookId, bookTitle, pageNumber, chunkId);
    }
}
