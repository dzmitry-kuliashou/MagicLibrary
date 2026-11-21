using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Exceptions;
using MagicLibrary.Core.Repositories;
using MagicLibrary.Infrastructure.StorageModels;
using JasperFx;
using Marten;
using Marten.Exceptions;

namespace MagicLibrary.Infrastructure.Repositories;

internal abstract class WriteRepository<TAggregate, TDocument>(IDocumentSession documentSession)
    : IWriteRepository<TAggregate>
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
