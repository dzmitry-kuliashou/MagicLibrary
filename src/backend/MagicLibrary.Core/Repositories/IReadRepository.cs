using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Core.Repositories;

public interface IReadRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TAggregate>> ListAsync(int skip, int take, CancellationToken cancellationToken);
}
