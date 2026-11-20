namespace MagicLibrary.Core.Aggregates;

public abstract class AggregateRoot
{
    public Guid Id { get; private set; } = Guid.NewGuid();
}
