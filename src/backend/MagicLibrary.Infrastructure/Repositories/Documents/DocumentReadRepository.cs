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

    public async Task<(IReadOnlyList<TAggregate> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var skip = (int)Math.Min((long)page * pageSize, int.MaxValue);
        var query = querySession.Query<TDocument>();
        var totalCount = await query.CountAsync(cancellationToken);
        var documents = await query
            .OrderBy(document => document.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (documents.Select(ToAggregate).ToList(), totalCount);
    }
}
