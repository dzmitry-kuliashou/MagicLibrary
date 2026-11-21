using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Core.Repositories;

public interface IWriteRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    Task<TAggregate> CreateAsync(TAggregate aggregate, CancellationToken cancellationToken);

    Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken);
}
