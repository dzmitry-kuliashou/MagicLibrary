using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MagicLibrary.Infrastructure.KeywordExtraction;
using Xunit;

namespace MagicLibrary.UnitTests.KeywordExtraction;

public sealed class OllamaKeywordExtractorTests
{
    private const string ChatModel = "qwen3:4b";

    [Fact]
    public async Task ExtractAsync_ReturnsNoMoreThanConfiguredCount()
    {
        var (extractor, _) = CreateExtractor(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    message = new
                    {
                        content = "Search ranking\nSemantic indexing\nLocal inference\nDocument parsing"
                    }
                })
            }),
            maxResults: 3);

        var result = await extractor.ExtractAsync("Source text.", CancellationToken.None);

        Assert.Equal(
            ["Search ranking", "Semantic indexing", "Local inference"],
            result);
    }

    [Fact]
    public async Task ExtractAsync_DiscardsPhrasesLongerThanThreeWords()
    {
        var (extractor, _) = CreateExtractor(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    message = new
                    {
                        content = "semantic search\nlarge-scale semantic information retrieval\nbook indexing\nphrase with four words"
                    }
                })
            }));

        var result = await extractor.ExtractAsync("Source text.", CancellationToken.None);

        Assert.Equal(["semantic search", "book indexing"], result);
    }

    [Fact]
    public async Task ExtractAsync_PreservesReadablePhrasesWithoutNormalization()
    {
        var (extractor, _) = CreateExtractor(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    message = new
                    {
                        content = "- Semantic Search!\n2. Book indexing, AI!"
                    }
                })
            }));

        var result = await extractor.ExtractAsync("Source text.", CancellationToken.None);

        Assert.Equal(["Semantic Search!", "Book indexing, AI!"], result);
    }

    [Fact]
    public async Task ExtractAsync_UsesConfiguredChatModelAndEndpoint()
    {
        var (extractor, handler) = CreateExtractor(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    message = new { content = "semantic search" }
                })
            }));

        await extractor.ExtractAsync("Source chunk.", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(new Uri("http://localhost:11435/api/chat"), handler.RequestUri);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(ChatModel, request.RootElement.GetProperty("model").GetString());
        Assert.False(request.RootElement.GetProperty("stream").GetBoolean());
        Assert.Contains(
            "Source chunk.",
            request.RootElement.GetProperty("messages")[0].GetProperty("content").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractAsync_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var (extractor, handler) = CreateExtractor(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var extractionTask = extractor.ExtractAsync("Source chunk.", cancellationSource.Token);
        await handler.RequestStarted.Task;
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractionTask);
        Assert.True(handler.RequestCancellationToken.HasValue);
        Assert.True(handler.RequestCancellationToken.Value.IsCancellationRequested);
    }

    private static (
        OllamaKeywordExtractor Extractor,
        FakeHttpMessageHandler Handler) CreateExtractor(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory,
        int maxResults = 3)
    {
        var handler = new FakeHttpMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11435")
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Chat:Model"] = ChatModel
            })
            .Build();
        var options = Options.Create(new KeywordExtractionOptions
        {
            MaxResults = maxResults
        });

        return (new OllamaKeywordExtractor(httpClient, options, configuration), handler);
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
