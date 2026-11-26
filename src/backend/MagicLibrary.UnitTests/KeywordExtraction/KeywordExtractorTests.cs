using System.Text.Json;
using MagicLibrary.Infrastructure.KeywordExtraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.KeywordExtraction;

public sealed class KeywordExtractorTests
{
    [Fact]
    public async Task ExtractAsync_ReturnsNoMoreThanConfiguredCount()
    {
        var extractor = CreateExtractor(
            (_, _, _) => Task.FromResult(CreateResponse(
                "Search ranking",
                "Semantic indexing",
                "Local inference",
                "Document parsing")),
            maxResults: 3);

        var result = await extractor.ExtractAsync("Source text.", CancellationToken.None);

        Assert.Equal(
            ["Search ranking", "Semantic indexing", "Local inference"],
            result);
    }

    [Fact]
    public async Task ExtractAsync_DiscardsPhrasesLongerThanThreeWords()
    {
        var extractor = CreateExtractor(
            (_, _, _) => Task.FromResult(CreateResponse(
                "semantic search",
                "large-scale semantic information retrieval",
                "book indexing",
                "phrase with four words")));

        var result = await extractor.ExtractAsync("Source text.", CancellationToken.None);

        Assert.Equal(["semantic search", "book indexing"], result);
    }

    [Fact]
    public async Task ExtractAsync_PreservesReadablePhrasesWithoutNormalization()
    {
        var extractor = CreateExtractor(
            (_, _, _) => Task.FromResult(CreateResponse(
                "Semantic Search!",
                "Book indexing, AI!")));

        var result = await extractor.ExtractAsync("Source text.", CancellationToken.None);

        Assert.Equal(["Semantic Search!", "Book indexing, AI!"], result);
    }

    [Fact]
    public async Task ExtractAsync_UsesConfiguredJsonOptionsAndPrompt()
    {
        const string text = "Source chunk.";
        IEnumerable<ChatMessage>? receivedMessages = null;
        ChatOptions? receivedOptions = null;
        var extractor = CreateExtractor(
            (messages, options, _) =>
            {
                receivedMessages = messages.ToArray();
                receivedOptions = options;
                return Task.FromResult(CreateResponse("semantic search"));
            });

        await extractor.ExtractAsync(text, CancellationToken.None);

        var message = Assert.Single(receivedMessages!);
        Assert.Equal(ChatRole.User, message.Role);
        Assert.Contains(text, message.Text, StringComparison.Ordinal);
        Assert.Equal(128, receivedOptions?.MaxOutputTokens);
        Assert.Equal(ReasoningEffort.None, receivedOptions?.Reasoning?.Effort);
        Assert.Same(ChatResponseFormat.Json, receivedOptions?.ResponseFormat);
    }

    [Fact]
    public async Task ExtractAsync_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var requestStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var extractor = CreateExtractor(
            async (_, _, cancellationToken) =>
            {
                requestStarted.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return CreateResponse("semantic search");
            });

        var extractionTask = extractor.ExtractAsync("Source chunk.", cancellationSource.Token);
        await requestStarted.Task;
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_EmptyResponseContent_ReportsInvalidResponse()
    {
        var extractor = CreateExtractor(
            (_, _, _) => Task.FromResult(CreateResponseWithContent(string.Empty)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync("Source chunk.", CancellationToken.None));

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_MalformedJson_PropagatesJsonException()
    {
        var extractor = CreateExtractor(
            (_, _, _) => Task.FromResult(CreateResponseWithContent("{ invalid json")));

        await Assert.ThrowsAsync<JsonException>(
            () => extractor.ExtractAsync("Source chunk.", CancellationToken.None));
    }

    [Fact]
    public async Task ExtractAsync_ResponseWithoutKeywords_ReportsInvalidResponse()
    {
        var extractor = CreateExtractor(
            (_, _, _) => Task.FromResult(CreateResponseWithContent("""{"other":[]}""")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync("Source chunk.", CancellationToken.None));

        Assert.Contains("invalid keyword response", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_ClientFailure_PropagatesOriginalException()
    {
        var expectedException = new InvalidOperationException("Chat client failure.");
        var extractor = CreateExtractor(
            (_, _, _) => Task.FromException<ChatResponse>(expectedException));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync("Source chunk.", CancellationToken.None));

        Assert.Same(expectedException, exception);
    }

    private static KeywordExtractor CreateExtractor(
        Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> responseFactory,
        int maxResults = 3)
    {
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? options,
                    CancellationToken cancellationToken) =>
                responseFactory(messages, options, cancellationToken));

        var options = Options.Create(new KeywordExtractionOptions
        {
            MaxResults = maxResults
        });

        return new KeywordExtractor(client.Object, options);
    }

    private static ChatResponse CreateResponse(params string[] keywords) =>
        new(new ChatMessage(
            ChatRole.Assistant,
            JsonSerializer.Serialize(new { keywords })));

    private static ChatResponse CreateResponseWithContent(string content) =>
        new(new ChatMessage(ChatRole.Assistant, content));
}
