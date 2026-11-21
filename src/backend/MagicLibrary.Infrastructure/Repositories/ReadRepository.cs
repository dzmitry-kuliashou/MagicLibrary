using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories;
using MagicLibrary.Infrastructure.StorageModels;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories;

internal abstract class ReadRepository<TAggregate, TDocument> : IReadRepository<TAggregate>
    where TAggregate : AggregateRoot
    where TDocument : StorageDocument
{
    private readonly IQuerySession _querySession;

    protected ReadRepository(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    protected abstract TAggregate ToAggregate(TDocument document);

    public async Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _querySession.LoadAsync<TDocument>(id, cancellationToken);
        return document is null ? null : ToAggregate(document);
    }

    public async Task<IReadOnlyList<TAggregate>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var documents = await _querySession.Query<TDocument>()
            .OrderBy(document => document.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return documents.Select(ToAggregate).ToList();
    }
}
