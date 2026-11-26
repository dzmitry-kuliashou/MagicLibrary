using System.Net;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Infrastructure.EmbeddingGeneration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using AiEmbedding = Microsoft.Extensions.AI.Embedding<float>;
using AiEmbeddingGenerator = Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>;

namespace MagicLibrary.UnitTests.EmbeddingGeneration;

public sealed class EmbeddingGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ValidText_DoesNotOverrideClientModel()
    {
        const string text = "A page of source text.";
        var expectedVector = new[] { 0.1f, 0.2f, 0.3f };
        EmbeddingGenerationOptions? receivedOptions = null;
        var (generator, client) = CreateGenerator(
            (values, options, _) =>
            {
                Assert.Equal([text], values);
                receivedOptions = options;
                return Task.FromResult(CreateEmbeddings(expectedVector));
            });

        var result = await generator.GenerateAsync(text, CancellationToken.None);

        Assert.Equal(expectedVector, result);
        Assert.Null(receivedOptions?.ModelId);
        client.Verify(
            service => service.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_ClientFailure_PropagatesProviderFailure()
    {
        var (generator, _) = CreateGenerator(
            (_, _, _) => Task.FromException<GeneratedEmbeddings<AiEmbedding>>(
                new HttpRequestException("Provider failure.", null, HttpStatusCode.ServiceUnavailable)));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => generator.GenerateAsync("Text", CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Fact]
    public async Task GenerateAsync_DimensionMismatch_ReportsInvalidResponse()
    {
        var (generator, _) = CreateGenerator(
            (_, _, _) => Task.FromResult(CreateEmbeddings([0.1f, 0.2f])));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync("Text", CancellationToken.None));

        Assert.Contains("2 dimensions", exception.Message, StringComparison.Ordinal);
        Assert.Contains("expected 3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_CancellationRequested_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var generationStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var (generator, _) = CreateGenerator(
            async (_, _, cancellationToken) =>
            {
                generationStarted.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return CreateEmbeddings([0.1f, 0.2f, 0.3f]);
            });

        var generationTask = generator.GenerateAsync("Text", cancellationSource.Token);
        await generationStarted.Task;
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generationTask);
    }

    private static EmbeddingOptions CreateOptions() =>
        new()
        {
            Dimensions = 3
        };

    private static (
        EmbeddingGenerator Generator,
        Mock<AiEmbeddingGenerator> Client) CreateGenerator(
        Func<IEnumerable<string>, EmbeddingGenerationOptions?, CancellationToken,
            Task<GeneratedEmbeddings<AiEmbedding>>> generateAsync,
        EmbeddingOptions? options = null)
    {
        var client = new Mock<AiEmbeddingGenerator>();
        client
            .Setup(service => service.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> values, EmbeddingGenerationOptions? generationOptions,
                    CancellationToken cancellationToken) =>
                generateAsync(values, generationOptions, cancellationToken));

        return (
            new EmbeddingGenerator(
                client.Object,
                Options.Create(options ?? CreateOptions())),
            client);
    }

    private static GeneratedEmbeddings<AiEmbedding> CreateEmbeddings(float[] vector) =>
        new([new AiEmbedding(vector)]);
}
