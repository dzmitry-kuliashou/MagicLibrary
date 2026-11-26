using MagicLibrary.Application.ConfigurationOptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using AiEmbeddingGenerator = Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>;

namespace MagicLibrary.Infrastructure.EmbeddingGeneration;

public sealed class EmbeddingGenerator(
    AiEmbeddingGenerator embeddingGenerator,
    IOptions<EmbeddingOptions> options)
    : MagicLibrary.Application.EmbeddingGeneration.IEmbeddingGenerator
{
    public async Task<IReadOnlyList<float>> GenerateAsync(
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var embeddingOptions = options.Value;

        var vector = await embeddingGenerator.GenerateVectorAsync(
            text,
            cancellationToken: cancellationToken);
        if (vector.Length != embeddingOptions.Dimensions)
        {
            throw new InvalidOperationException(
                $"The embedding provider returned a vector with {vector.Length} dimensions; " +
                $"expected {embeddingOptions.Dimensions}.");
        }

        return vector.ToArray();
    }
}
