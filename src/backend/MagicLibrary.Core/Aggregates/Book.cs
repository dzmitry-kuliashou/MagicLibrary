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
        Guard.Against.NullOrWhiteSpace(sourceFileName, nameof(sourceFileName));
        Guard.Against.NullOrWhiteSpace(title, nameof(title));
        Guard.Against.Null(author, nameof(author));
        Guard.Against.NullOrWhiteSpace(contentHash, nameof(contentHash));
        Guard.Against.Default(addedAt, nameof(addedAt));

        return new Book(sourceFileName, title, author, contentHash, addedAt);
    }

    public void UpdateMetadata(string title, string author)
    {
        Guard.Against.NullOrWhiteSpace(title, nameof(title));
        Guard.Against.Null(author, nameof(author));

        Title = title;
        Author = author;
    }

    public void AddKeywords(IEnumerable<string> keywords)
    {
        Guard.Against.Null(keywords, nameof(keywords));
        _keywords.UnionWith(keywords);
    }

    public void MarkIndexingStarted()
    {
        IndexingStatus = BookIndexingStatus.InProgress;
        IndexedAt = null;
    }

    public void MarkIndexed(DateTimeOffset indexedAt)
    {
        Guard.Against.Default(indexedAt, nameof(indexedAt));
        IndexingStatus = BookIndexingStatus.Indexed;
        IndexedAt = indexedAt;
    }

    public void MarkIndexingFailed()
    {
        IndexingStatus = BookIndexingStatus.Failed;
        IndexedAt = null;
    }
}
