using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories;
using MagicLibrary.Infrastructure.StorageModels;
using MagicLibrary.Infrastructure.StorageModels.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories;

internal sealed class BookReadRepository(IQuerySession querySession)
    : ReadRepository<Book, BookDocument>(querySession), IBookReadRepository
{
    protected override Book ToAggregate(BookDocument document) =>
        BookDocumentMapper.ToAggregate(document);
}
