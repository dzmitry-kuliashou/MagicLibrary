using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using Microsoft.Extensions.Options;

namespace MagicLibrary.Infrastructure.EmbeddingGeneration;

public sealed class EmbeddingGeneratorFactory(
    IOptions<EmbeddingOptions> options,
    IEnumerable<IEmbeddingGenerator> generators) : IEmbeddingGeneratorFactory
{
    public IEmbeddingGenerator Create()
    {
        var configuredProvider = options.Value.Provider;
        return generators.FirstOrDefault(generator =>
                   string.Equals(
                       configuredProvider,
                       generator.Provider,
                       StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException(
                   $"No embedding generator is registered for provider '{configuredProvider}'.");
    }
}
