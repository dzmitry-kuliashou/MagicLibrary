namespace MagicLibrary.Application.EmbeddingGeneration;

public interface IEmbeddingGenerator
{
    string Provider { get; }

    Task<IReadOnlyList<float>> GenerateAsync(
        string text,
        CancellationToken cancellationToken);
}
