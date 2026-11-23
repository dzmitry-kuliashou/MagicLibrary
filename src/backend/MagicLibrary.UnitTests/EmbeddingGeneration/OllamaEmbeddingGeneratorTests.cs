using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Infrastructure.EmbeddingGeneration;
using Microsoft.Extensions.Options;
using Xunit;

namespace MagicLibrary.UnitTests.EmbeddingGeneration;

public sealed class OllamaEmbeddingGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ValidText_SendsConfiguredModelAndReturnsVector()
    {
        const string text = "A page of source text.";
        var expectedVector = new[] { 0.1f, 0.2f, 0.3f };
        var (generator, handler) = CreateGenerator(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { embeddings = new[] { expectedVector } })
            }));

        var result = await generator.GenerateAsync(text, CancellationToken.None);

        Assert.Equal(expectedVector, result);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(new Uri("http://localhost:11435/api/embed"), handler.RequestUri);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("nomic-embed-text:latest", request.RootElement.GetProperty("model").GetString());
        Assert.Equal(text, request.RootElement.GetProperty("input").GetString());
    }

    [Fact]
    public async Task GenerateAsync_UnsupportedProvider_ReportsConfigurationError()
    {
        var (generator, handler) = CreateGenerator(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)),
            CreateOptions("Unsupported"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync("Text", CancellationToken.None));

        Assert.Contains("Unsupported", exception.Message, StringComparison.Ordinal);
        Assert.Null(handler.Method);
    }

    [Fact]
    public async Task GenerateAsync_HttpFailure_ReportsProviderFailure()
    {
        var (generator, _) = CreateGenerator(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => generator.GenerateAsync("Text", CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"embeddings\":[]}")]
    [InlineData("{\"embeddings\":[[]]}")]
    public async Task GenerateAsync_InvalidResponse_ReportsInvalidResponse(string responseBody)
    {
        var (generator, _) = CreateGenerator(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            }));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync("Text", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_CancellationRequested_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var (generator, handler) = CreateGenerator(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var generationTask = generator.GenerateAsync("Text", cancellationSource.Token);
        await handler.RequestStarted.Task;
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generationTask);
        Assert.True(handler.RequestCancellationToken.HasValue);
        Assert.True(handler.RequestCancellationToken.Value.IsCancellationRequested);
    }

    [Fact]
    public async Task GenerateAsync_DimensionMismatch_ReportsInvalidResponse()
    {
        var (generator, _) = CreateGenerator(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    embeddings = new[] { new[] { 0.1f, 0.2f } }
                })
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync("Text", CancellationToken.None));

        Assert.Contains("2 dimensions", exception.Message, StringComparison.Ordinal);
        Assert.Contains("expected 3", exception.Message, StringComparison.Ordinal);
    }

    private static EmbeddingOptions CreateOptions(string provider = "Ollama")
    {
        return new EmbeddingOptions
        {
            Provider = provider,
            BaseUrl = "http://localhost:11435",
            Model = "nomic-embed-text:latest",
            Dimensions = 3
        };
    }

    private static (OllamaEmbeddingGenerator Generator, FakeHttpMessageHandler Handler) CreateGenerator(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory,
        EmbeddingOptions? options = null)
    {
        var embeddingOptions = options ?? CreateOptions();
        var handler = new FakeHttpMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(embeddingOptions.BaseUrl)
        };

        return (
            new OllamaEmbeddingGenerator(httpClient, Options.Create(embeddingOptions)),
            handler);
    }

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        public TaskCompletionSource<bool> RequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        public CancellationToken? RequestCancellationToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            RequestCancellationToken = cancellationToken;
            RequestStarted.TrySetResult(true);
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return await responseFactory(request, cancellationToken);
        }
    }
}
