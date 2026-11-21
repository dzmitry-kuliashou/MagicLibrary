using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories;
using MagicLibrary.Infrastructure.StorageModels;
using MagicLibrary.Infrastructure.StorageModels.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories;

internal sealed class BookWriteRepository(IDocumentSession documentSession)
    : WriteRepository<Book, BookDocument>(documentSession), IBookWriteRepository
{
    protected override BookDocument ToDocument(Book aggregate) =>
        BookDocumentMapper.ToDocument(aggregate);
}
