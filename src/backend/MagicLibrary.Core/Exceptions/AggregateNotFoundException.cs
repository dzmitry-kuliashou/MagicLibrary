namespace MagicLibrary.Core.Exceptions;

public class AggregateNotFoundException : Exception
{
    public AggregateNotFoundException(Guid aggregateId)
        : this(aggregateId, $"An aggregate with id '{aggregateId}' was not found.")
    {
    }

    private AggregateNotFoundException(Guid aggregateId, string message)
        : base(message)
    {
        AggregateId = aggregateId;
    }

    public Guid AggregateId { get; }
}
