namespace MagicLibrary.Core.Repositories.Vectors;

public interface IVectorReadRepository<TRecord>
{
    Task<IReadOnlyList<TRecord>> SearchAsync(
        IReadOnlyList<float> queryEmbedding,
        int topK,
        Guid? scopeId,
        CancellationToken cancellationToken);
}
