using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories.Documents;

internal sealed class BookReadRepository(IQuerySession querySession)
    : DocumentReadRepository<Book, BookDocument>(querySession), IBookReadRepository
{
    protected override Book ToAggregate(BookDocument document) =>
        BookDocumentMapper.ToAggregate(document);
}
