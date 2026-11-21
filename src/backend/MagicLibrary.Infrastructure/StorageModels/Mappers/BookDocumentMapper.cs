using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Infrastructure.StorageModels.Mappers;

internal static class BookDocumentMapper
{
    internal static BookDocument ToDocument(Book book)
    {
        return new BookDocument
        {
            Id = book.Id,
            SourceFileName = book.SourceFileName,
            Title = book.Title,
            Author = book.Author,
            Keywords = book.Keywords.ToList(),
            ContentHash = book.ContentHash,
            AddedAt = book.AddedAt,
            IndexedAt = book.IndexedAt,
            IndexingStatus = book.IndexingStatus
        };
    }

    internal static Book ToAggregate(BookDocument document)
    {
        return Book.Reconstitute(
            document.Id,
            document.SourceFileName,
            document.Title,
            document.Author,
            document.ContentHash,
            document.AddedAt,
            document.Keywords,
            document.IndexedAt,
            document.IndexingStatus);
    }
}
