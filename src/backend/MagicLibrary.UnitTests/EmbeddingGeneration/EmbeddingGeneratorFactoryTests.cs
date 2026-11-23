using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Infrastructure.EmbeddingGeneration;
using Microsoft.Extensions.Options;
using Xunit;

namespace MagicLibrary.UnitTests.EmbeddingGeneration;

public sealed class EmbeddingGeneratorFactoryTests
{
    [Fact]
    public void Create_OllamaProvider_ReturnsOllamaGenerator()
    {
        var otherGenerator = new StubEmbeddingGenerator("Other");
        var ollamaGenerator = new StubEmbeddingGenerator("Ollama");
        var factory = new EmbeddingGeneratorFactory(
            Options.Create(new EmbeddingOptions { Provider = "Ollama" }),
            [otherGenerator, ollamaGenerator]);

        var result = factory.Create();

        Assert.Same(ollamaGenerator, result);
    }

    [Fact]
    public void Create_UnsupportedProvider_ThrowsConfigurationError()
    {
        var factory = new EmbeddingGeneratorFactory(
            Options.Create(new EmbeddingOptions { Provider = "Unsupported" }),
            [new StubEmbeddingGenerator("Ollama")]);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Create());

        Assert.Contains("Unsupported", exception.Message, StringComparison.Ordinal);
    }

    private sealed class StubEmbeddingGenerator(string provider) : IEmbeddingGenerator
    {
        public string Provider => provider;

        public Task<IReadOnlyList<float>> GenerateAsync(
            string text,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<float>>([]);
        }
    }
}
