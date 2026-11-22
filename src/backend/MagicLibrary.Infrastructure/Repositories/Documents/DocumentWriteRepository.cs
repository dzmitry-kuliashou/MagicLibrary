using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Exceptions;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using JasperFx;
using Marten;
using Marten.Exceptions;
using Npgsql;

namespace MagicLibrary.Infrastructure.Repositories.Documents;

internal abstract class DocumentWriteRepository<TAggregate, TDocument>(IDocumentSession documentSession)
    : IDocumentWriteRepository<TAggregate>
    where TAggregate : AggregateRoot
    where TDocument : StorageDocument
{
    protected abstract TDocument ToDocument(TAggregate aggregate);

    public async Task<TAggregate> CreateAsync(TAggregate aggregate, CancellationToken cancellationToken)
    {
        try
        {
            documentSession.Insert(ToDocument(aggregate));
            await documentSession.SaveChangesAsync(cancellationToken);
        }
        catch (DocumentAlreadyExistsException)
        {
            throw new AggregateAlreadyExistsException(aggregate.Id);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new AggregateAlreadyExistsException(aggregate.Id);
        }

        return aggregate;
    }

    public async Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken)
    {
        try
        {
            documentSession.Update(ToDocument(aggregate));
            await documentSession.SaveChangesAsync(cancellationToken);
        }
        catch (NonExistentDocumentException)
        {
            throw new AggregateNotFoundException(aggregate.Id);
        }

        return aggregate;
    }
}
