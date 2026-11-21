namespace MagicLibrary.Core.Aggregates;

public abstract class AggregateRoot
{
    protected AggregateRoot()
        : this(Guid.NewGuid())
    {
    }

    protected AggregateRoot(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; private set; }
}
