using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Infrastructure.StorageModels;

public sealed class BookDocument : StorageDocument
{
    public string SourceFileName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public List<string> Keywords { get; set; } = [];

    public string ContentHash { get; set; } = string.Empty;

    public DateTimeOffset AddedAt { get; set; }

    public DateTimeOffset? IndexedAt { get; set; }

    public BookIndexingStatus IndexingStatus { get; set; }
}
