namespace MagicLibrary.Application.EmbeddingGeneration;

public interface IEmbeddingGenerator
{
    Task<IReadOnlyList<float>> GenerateAsync(
        string text,
        CancellationToken cancellationToken);
}
