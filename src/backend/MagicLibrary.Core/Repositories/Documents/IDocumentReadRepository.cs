using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Core.Repositories.Documents;

public interface IDocumentReadRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TAggregate>> ListAsync(int skip, int take, CancellationToken cancellationToken);
}
