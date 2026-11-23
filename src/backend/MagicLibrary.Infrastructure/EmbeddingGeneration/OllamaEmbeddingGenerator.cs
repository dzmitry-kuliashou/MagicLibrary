using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using Microsoft.Extensions.Options;

namespace MagicLibrary.Infrastructure.EmbeddingGeneration;

public sealed class OllamaEmbeddingGenerator(
    HttpClient httpClient,
    IOptions<EmbeddingOptions> options) : IEmbeddingGenerator
{
    public string Provider => "Ollama";

    public async Task<IReadOnlyList<float>> GenerateAsync(
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var embeddingOptions = options.Value;
        if (!string.Equals(embeddingOptions.Provider, Provider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Embedding provider '{embeddingOptions.Provider}' is not supported. Only '{Provider}' is supported.");
        }

        using var response = await httpClient.PostAsJsonAsync(
            "api/embed",
            new OllamaEmbedRequest(embeddingOptions.Model, text),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(
            cancellationToken: cancellationToken);
        if (result?.Embeddings is not { Length: 1 } embeddings ||
            embeddings[0] is not { Length: > 0 } vector)
        {
            throw new InvalidOperationException("Ollama returned an invalid embedding response.");
        }

        if (vector.Length != embeddingOptions.Dimensions)
        {
            throw new InvalidOperationException(
                $"Ollama returned an embedding with {vector.Length} dimensions; " +
                $"expected {embeddingOptions.Dimensions}.");
        }

        return vector;
    }

    private sealed record OllamaEmbedRequest(string Model, string Input);

    private sealed record OllamaEmbedResponse(
        [property: JsonPropertyName("embeddings")] float[][]? Embeddings);
}
