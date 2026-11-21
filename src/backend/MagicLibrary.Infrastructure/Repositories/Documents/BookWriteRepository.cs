using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories.Documents;

internal sealed class BookWriteRepository(IDocumentSession documentSession)
    : DocumentWriteRepository<Book, BookDocument>(documentSession), IBookWriteRepository
{
    protected override BookDocument ToDocument(Book aggregate) =>
        BookDocumentMapper.ToDocument(aggregate);
}
