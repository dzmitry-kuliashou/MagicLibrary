using MagicLibrary.Infrastructure.Chat;
using MagicLibrary.Infrastructure.KeywordExtraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Infrastructure.Chat;

public sealed class BookSearchTermExtractorTests
{
    [Fact]
    public async Task ExtractAsync_PreservesExactNamesFromProvidedMessage()
    {
        const string userMessage = "Find The Left Hand of Darkness by Ursula K. Le Guin.";
        ChatMessage[]? receivedMessages = null;
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
            {
                receivedMessages = messages.ToArray();
                return Task.FromResult(new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    """{"exactTitleTerms":["The Left Hand of Darkness"],"exactAuthorTerms":["Ursula K. Le Guin"],"keywordPhrases":["science fiction"]}""")));
            });
        var extractor = CreateExtractor(client.Object);

        var result = await extractor.ExtractAsync(userMessage, CancellationToken.None);

        var messages = receivedMessages
            ?? throw new InvalidOperationException("The chat client was not called.");
        Assert.Equal(2, messages.Length);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Contains(
            "cannot confirm it is a known book",
            messages[0].Text,
            StringComparison.Ordinal);
        var suppliedMessage = messages[1];
        Assert.Equal(ChatRole.User, suppliedMessage.Role);
        Assert.Equal(userMessage, suppliedMessage.Text);
        Assert.Equal(["The Left Hand of Darkness"], result.ExactTitleTerms);
        Assert.Equal(["Ursula K. Le Guin"], result.ExactAuthorTerms);
        Assert.Equal(["science fiction"], result.KeywordPhrases);
    }

    [Theory]
    [InlineData("""{"exactTitleTerms":[],"exactAuthorTerms":[],"keywordPhrases":[]}""")]
    [InlineData("""{"exactAuthorTerms":[],"keywordPhrases":[]}""")]
    [InlineData("""{"exactTitleTerms":null,"exactAuthorTerms":[],"keywordPhrases":[]}""")]
    [InlineData("""{"exactTitleTerms":[],"keywordPhrases":[]}""")]
    [InlineData("""{"exactTitleTerms":[],"exactAuthorTerms":null,"keywordPhrases":[]}""")]
    [InlineData("""{"exactTitleTerms":[],"exactAuthorTerms":[]}""")]
    [InlineData("""{"exactTitleTerms":[],"exactAuthorTerms":[],"keywordPhrases":null}""")]
    public async Task ExtractAsync_NoBookSearchTerms_ReturnsEmptyResult(string responseContent)
    {
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                responseContent)));
        var extractor = CreateExtractor(client.Object);

        var result = await extractor.ExtractAsync("Recommend a book.", CancellationToken.None);

        Assert.False(result.HasSearchTerms);
        Assert.Empty(result.ExactTitleTerms);
        Assert.Empty(result.ExactAuthorTerms);
        Assert.Empty(result.KeywordPhrases);
    }

    [Fact]
    public async Task ExtractAsync_SkipsInvalidTermsAndCapsOverlongKeywords()
    {
        var responseContent =
            """{"exactTitleTerms":["The Dispossessed","The Left Hand of Darkness","",null],"exactAuthorTerms":["Ursula K. Le Guin","Octavia Butler",null],"keywordPhrases":["  feminist science fiction  ","phrase with too many words"," ",null]}""";
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken _) =>
                Task.FromResult(new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    responseContent))));
        var extractor = CreateExtractor(client.Object);

        var result = await extractor.ExtractAsync(
            "Read The Dispossessed by Ursula K. Le Guin.",
            CancellationToken.None);

        Assert.Equal(["The Dispossessed"], result.ExactTitleTerms);
        Assert.Equal(["Ursula K. Le Guin"], result.ExactAuthorTerms);
        Assert.Equal(["feminist science fiction", "phrase with too"], result.KeywordPhrases);

        responseContent = "{ invalid json";
        var invalidJsonResult = await extractor.ExtractAsync(
            "Read The Dispossessed by Ursula K. Le Guin.",
            CancellationToken.None);
        Assert.False(invalidJsonResult.HasSearchTerms);
    }

    [Fact]
    public async Task ExtractAsync_CapsKeywordPhrasesAtConfiguredMaximum()
    {
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                """{"exactTitleTerms":[],"exactAuthorTerms":[],"keywordPhrases":["first phrase","second phrase","third phrase"]}""")));
        var extractor = new BookSearchTermExtractor(
            client.Object,
            Options.Create(new KeywordExtractionOptions { MaxResults = 2 }));

        var result = await extractor.ExtractAsync("Recommend a book.", CancellationToken.None);

        Assert.Equal(["first phrase", "second phrase"], result.KeywordPhrases);
    }

    [Fact]
    public async Task ExtractAsync_WrongResponseShape_ReturnsEmptyResult()
    {
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                """{"index":0,"role":"system","content":"An echoed system message."}""")));
        var extractor = CreateExtractor(client.Object);

        var result = await extractor.ExtractAsync("Recommend a book.", CancellationToken.None);

        Assert.False(result.HasSearchTerms);
        Assert.Empty(result.ExactTitleTerms);
        Assert.Empty(result.ExactAuthorTerms);
        Assert.Empty(result.KeywordPhrases);
    }

    [Fact]
    public void ResponseFormat_RequiresExpectedSearchTermProperties()
    {
        var responseFormat = Assert.IsType<ChatResponseFormatJson>(
            BookSearchTermExtractor.ResponseFormat);
        var schema = responseFormat.Schema
            ?? throw new InvalidOperationException("The book-search response schema is missing.");
        var requiredProperties = schema.GetProperty("required")
            .EnumerateArray()
            .Select(property => property.GetString())
            .ToArray();

        Assert.Contains("exactTitleTerms", requiredProperties);
        Assert.Contains("exactAuthorTerms", requiredProperties);
        Assert.Contains("keywordPhrases", requiredProperties);

        var properties = schema.GetProperty("properties");
        foreach (var propertyName in new[]
                 {
                     "exactTitleTerms",
                     "exactAuthorTerms",
                     "keywordPhrases"
                 })
        {
            var arraySchema = properties.GetProperty(propertyName);
            Assert.Equal("array", arraySchema.GetProperty("type").GetString());
            Assert.Equal("string", arraySchema.GetProperty("items").GetProperty("type").GetString());
        }
    }

    private static BookSearchTermExtractor CreateExtractor(IChatClient chatClient) =>
        new(chatClient, Options.Create(new KeywordExtractionOptions()));
}
