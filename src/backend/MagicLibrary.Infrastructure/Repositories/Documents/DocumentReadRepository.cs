using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories.Documents;

internal abstract class DocumentReadRepository<TAggregate, TDocument>(IQuerySession querySession)
    : IDocumentReadRepository<TAggregate>
    where TAggregate : AggregateRoot
    where TDocument : StorageDocument
{
    protected abstract TAggregate ToAggregate(TDocument document);

    public async Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await querySession.LoadAsync<TDocument>(id, cancellationToken);
        return document is null ? null : ToAggregate(document);
    }

    public async Task<IReadOnlyList<TAggregate>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var documents = await querySession.Query<TDocument>()
            .OrderBy(document => document.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return documents.Select(ToAggregate).ToList();
    }
}
