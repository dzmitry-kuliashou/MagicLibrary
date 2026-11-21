namespace MagicLibrary.Core.Repositories.Vectors;

public interface IVectorWriteRepository<in TRecord>
{
    Task ReplaceAsync(
        Guid scopeId,
        IReadOnlyCollection<TRecord> records,
        CancellationToken cancellationToken);
}
