using Ardalis.GuardClauses;

namespace MagicLibrary.Core.Aggregates;

public enum BookIndexingStatus
{
    Pending,
    InProgress,
    Indexed,
    Failed
}

public sealed class Book : AggregateRoot
{
    private readonly HashSet<string> _keywords = new(StringComparer.Ordinal);

    private Book(
        string sourceFileName,
        string title,
        string author,
        string contentHash,
        DateTimeOffset addedAt)
    {
        SourceFileName = sourceFileName;
        Title = title;
        Author = author;
        ContentHash = contentHash;
        AddedAt = addedAt;
        IndexingStatus = BookIndexingStatus.Pending;
    }

    private Book(
        Guid id,
        string sourceFileName,
        string title,
        string author,
        string contentHash,
        DateTimeOffset addedAt,
        IEnumerable<string> keywords,
        DateTimeOffset? indexedAt,
        BookIndexingStatus indexingStatus)
        : base(id)
    {
        SourceFileName = sourceFileName;
        Title = title;
        Author = author;
        ContentHash = contentHash;
        AddedAt = addedAt;
        _keywords.UnionWith(keywords);
        IndexedAt = indexedAt;
        IndexingStatus = indexingStatus;
    }

    public string SourceFileName { get; }

    public string Title { get; private set; }

    public string Author { get; private set; }

    public IReadOnlyCollection<string> Keywords => Array.AsReadOnly(_keywords.ToArray());

    public string ContentHash { get; }

    public DateTimeOffset AddedAt { get; }

    public DateTimeOffset? IndexedAt { get; private set; }

    public BookIndexingStatus IndexingStatus { get; private set; }

    public static Book Create(
        string sourceFileName,
        string title,
        string author,
        string contentHash,
        DateTimeOffset addedAt)
    {
        Guard.Against.NullOrWhiteSpace(sourceFileName);
        Guard.Against.NullOrWhiteSpace(title);
        Guard.Against.Null(author);
        Guard.Against.NullOrWhiteSpace(contentHash);
        Guard.Against.Default(addedAt);

        return new Book(sourceFileName, title, author, contentHash, addedAt);
    }

    internal static Book Reconstitute(
        Guid id,
        string sourceFileName,
        string title,
        string author,
        string contentHash,
        DateTimeOffset addedAt,
        IEnumerable<string> keywords,
        DateTimeOffset? indexedAt,
        BookIndexingStatus indexingStatus)
    {
        // Persisted state is trusted; write-side APIs validate new aggregates.
        return new Book(
            id,
            sourceFileName,
            title,
            author,
            contentHash,
            addedAt,
            keywords,
            indexedAt,
            indexingStatus);
    }

    public void UpdateMetadata(string title, string author)
    {
        Guard.Against.NullOrWhiteSpace(title);
        Guard.Against.Null(author);

        Title = title;
        Author = author;
    }

    public void AddKeywords(IEnumerable<string> keywords)
    {
        Guard.Against.Null(keywords);
        _keywords.UnionWith(keywords);
    }

    public void MarkIndexingStarted()
    {
        IndexingStatus = BookIndexingStatus.InProgress;
        IndexedAt = null;
    }

    public void MarkIndexed(DateTimeOffset indexedAt)
    {
        Guard.Against.Default(indexedAt);
        IndexingStatus = BookIndexingStatus.Indexed;
        IndexedAt = indexedAt;
    }

    public void MarkIndexingFailed()
    {
        IndexingStatus = BookIndexingStatus.Failed;
        IndexedAt = null;
    }
}
