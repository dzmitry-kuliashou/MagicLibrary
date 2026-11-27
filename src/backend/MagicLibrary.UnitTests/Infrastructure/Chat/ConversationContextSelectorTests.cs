using System.Text.Json;
using MagicLibrary.Application.Chat;
using MagicLibrary.Core.ValueObjects;
using MagicLibrary.Infrastructure.Chat;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Infrastructure.Chat;

public sealed class ConversationContextSelectorTests
{
    [Fact]
    public async Task SelectAsync_IndependentQuestion_UsesOnlyLatestUserMessage()
    {
        var sourceReference = SourceReference.Create(
            Guid.NewGuid(),
            "A Prior Book",
            4,
            Guid.NewGuid());
        var earlierUserMessage = Message.Create(MessageRole.User, "Tell me about book A.", []);
        var earlierAssistantMessage = Message.Create(
            MessageRole.Assistant,
            "Book A is about indexing.",
            [sourceReference]);
        var latestUserMessage = Message.Create(MessageRole.User, "How does the library work?", []);
        Message[] messages = [earlierUserMessage, earlierAssistantMessage, latestUserMessage];
        const string retrievalQuery = "How does the library work?";

        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                """{"selectedMessageIndices":[2],"retrievalQuery":"How does the library work?"}""")));

        var selector = new ConversationContextSelector(client.Object);

        var result = await selector.SelectAsync(messages, CancellationToken.None);

        Assert.Equal(latestUserMessage, Assert.Single(result.Messages));
        Assert.Equal(retrievalQuery, result.RetrievalQuery);
        Assert.Empty(result.PriorSourceReferences);
    }

    [Fact]
    public async Task SelectAsync_FollowUp_IncludesImmediatelyPrecedingExchange()
    {
        var sourceReference = SourceReference.Create(
            Guid.NewGuid(),
            "The Hobbit",
            15,
            Guid.NewGuid());
        var priorUserMessage = Message.Create(MessageRole.User, "Who wrote The Hobbit?", []);
        var priorAssistantMessage = Message.Create(
            MessageRole.Assistant,
            "J.R.R. Tolkien wrote The Hobbit.",
            [sourceReference]);
        var latestUserMessage = Message.Create(MessageRole.User, "What other books did he write?", []);
        Message[] messages = [priorUserMessage, priorAssistantMessage, latestUserMessage];
        const string retrievalQuery = "What other books did J.R.R. Tolkien write?";

        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                """{"selectedMessageIndices":[0,1,2],"retrievalQuery":"What other books did J.R.R. Tolkien write?"}""")));

        var selector = new ConversationContextSelector(client.Object);

        var result = await selector.SelectAsync(messages, CancellationToken.None);

        Assert.Equal([priorUserMessage, priorAssistantMessage, latestUserMessage], result.Messages);
        Assert.Equal(retrievalQuery, result.RetrievalQuery);
        Assert.Equal([sourceReference], result.PriorSourceReferences);
    }

    [Fact]
    public async Task SelectAsync_FollowUpNeedsOlderContext_IncludesOnlyRelevantOlderTurns()
    {
        var earthseaReference = SourceReference.Create(
            Guid.NewGuid(),
            "A Wizard of Earthsea",
            1,
            Guid.NewGuid());
        var isbnReference = SourceReference.Create(
            Guid.NewGuid(),
            "ISBN Guide",
            8,
            Guid.NewGuid());
        var earthseaUserMessage = Message.Create(
            MessageRole.User,
            "I'm asking about the Earthsea series by Ursula K. Le Guin.",
            []);
        var earthseaAssistantMessage = Message.Create(
            MessageRole.Assistant,
            "It begins with A Wizard of Earthsea.",
            [earthseaReference]);
        var isbnUserMessage = Message.Create(MessageRole.User, "How are ISBNs structured?", []);
        var isbnAssistantMessage = Message.Create(
            MessageRole.Assistant,
            "They contain a prefix, registration group, registrant, publication, and check digit.",
            [isbnReference]);
        var latestUserMessage = Message.Create(MessageRole.User, "How many books are in the series?", []);
        Message[] messages =
        [
            earthseaUserMessage,
            earthseaAssistantMessage,
            isbnUserMessage,
            isbnAssistantMessage,
            latestUserMessage
        ];
        const string retrievalQuery = "How many books are in Ursula K. Le Guin's Earthsea series?";

        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                """{"selectedMessageIndices":[0,1,4],"retrievalQuery":"How many books are in Ursula K. Le Guin's Earthsea series?"}""")));

        var selector = new ConversationContextSelector(client.Object);

        var result = await selector.SelectAsync(messages, CancellationToken.None);

        Assert.Equal([earthseaUserMessage, earthseaAssistantMessage, latestUserMessage], result.Messages);
        Assert.Equal(retrievalQuery, result.RetrievalQuery);
        Assert.Equal([earthseaReference], result.PriorSourceReferences);
    }

    [Fact]
    public async Task SelectAsync_SystemMessages_AreExcludedFromPromptAndOriginalIndicesArePreserved()
    {
        var systemMessage = Message.Create(MessageRole.System, "Do not send this system prompt.", []);
        var earlierUserMessage = Message.Create(MessageRole.User, "Earlier question.", []);
        var latestUserMessage = Message.Create(MessageRole.User, "Current question.", []);
        Message[] messages = [systemMessage, earlierUserMessage, latestUserMessage];
        ChatMessage[]? receivedMessages = null;
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> promptMessages, ChatOptions? _, CancellationToken _) =>
            {
                receivedMessages = promptMessages.ToArray();
                return Task.FromResult(new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    """{"selectedMessageIndices":[1,2],"retrievalQuery":"Current question"}""")));
            });
        var selector = new ConversationContextSelector(client.Object);

        var result = await selector.SelectAsync(messages, CancellationToken.None);

        var userPrompt = Assert.Single(receivedMessages!, message => message.Role == ChatRole.User);
        var promptText = userPrompt.Text
            ?? throw new InvalidOperationException("The context-selection prompt has no text.");
        Assert.DoesNotContain(systemMessage.Content, promptText, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(promptText);
        var serializedMessages = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, serializedMessages.Length);
        Assert.Equal(1, serializedMessages[0].GetProperty("index").GetInt32());
        Assert.Equal(2, serializedMessages[1].GetProperty("index").GetInt32());
        Assert.Equal([earlierUserMessage, latestUserMessage], result.Messages);
    }

    [Fact]
    public async Task SelectAsync_WrongResponseShape_FallsBackToLatestUserMessage()
    {
        var latestUserMessage = Message.Create(MessageRole.User, "Current question.", []);
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                """{"index":0,"role":"system","content":"An echoed system message."}""")));
        var selector = new ConversationContextSelector(client.Object);

        var result = await selector.SelectAsync([latestUserMessage], CancellationToken.None);

        Assert.Equal(latestUserMessage, Assert.Single(result.Messages));
        Assert.Equal(latestUserMessage.Content, result.RetrievalQuery);
        Assert.Empty(result.PriorSourceReferences);
    }

    [Fact]
    public void ResponseFormat_RequiresExpectedSelectionProperties()
    {
        var responseFormat = Assert.IsType<ChatResponseFormatJson>(
            ConversationContextSelector.ResponseFormat);
        var schema = responseFormat.Schema
            ?? throw new InvalidOperationException("The context selector response schema is missing.");
        var requiredProperties = schema.GetProperty("required")
            .EnumerateArray()
            .Select(property => property.GetString())
            .ToArray();

        Assert.Contains("selectedMessageIndices", requiredProperties);
        Assert.Contains("retrievalQuery", requiredProperties);
    }
}
