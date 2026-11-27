using MagicLibrary.Application.Chat;
using MagicLibrary.Core.ValueObjects;
using MagicLibrary.Infrastructure.Chat;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Infrastructure.Chat;

public sealed class LibraryChatAgentTests
{
    [Fact]
    public async Task StreamAsync_ExplicitBookId_RetrievesOnlyRequestedBookBeforeAnswering()
    {
        var requestedBookId = Guid.NewGuid();
        const string query = "A standalone retrieval query.";
        var sourceReference = SourceReference.Create(
            requestedBookId,
            "The Library Book",
            12,
            Guid.NewGuid());
        Message[] messages = [Message.Create(MessageRole.User, "Question.", [])];
        var selection = new ConversationContextSelection(messages, query, []);
        var (agent, search, client) = CreateAgent(selection);
        IReadOnlyCollection<Guid>? receivedBookIds = null;
        var callOrder = new List<string>();
        search
            .Setup(service => service.SearchChunksAsync(
                query,
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                callOrder.Add("chunks");
                receivedBookIds = bookIds;
                return Task.FromResult<IReadOnlyList<RetrievedLibraryChunk>>(
                    [new RetrievedLibraryChunk("Evidence.", sourceReference)]);
            });
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken _) =>
            {
                callOrder.Add("answer");
                return GetUpdates("Grounded answer.");
            });

        var updates = await StreamAsync(agent, messages, requestedBookId);

        Assert.Equal(["chunks", "answer"], callOrder);
        Assert.Equal([requestedBookId], receivedBookIds);
        search.Verify(
            service => service.SearchBooksAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(
            [sourceReference],
            Assert.Single(updates, update => update.SourceReferences is not null).SourceReferences);
    }

    [Fact]
    public async Task StreamAsync_SearchTermsComeOnlyFromLatestUserMessage()
    {
        const string latestUserMessage = "Find a book about the latest topic.";
        const string query = "The latest topic.";
        var bookId = Guid.NewGuid();
        Message[] messages =
        [
            Message.Create(MessageRole.User, "An older unrelated request.", []),
            Message.Create(MessageRole.Assistant, "An older response.", []),
            Message.Create(MessageRole.User, latestUserMessage, [])
        ];
        var selection = new ConversationContextSelection(messages, query, []);
        var (agent, search, _) = CreateAgent(selection);
        string? receivedMessage = null;
        IReadOnlyCollection<Guid>? receivedBookIds = null;
        search
            .Setup(service => service.SearchBooksAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((string message, CancellationToken _) =>
            {
                receivedMessage = message;
                return Task.FromResult<IReadOnlyList<Guid>>([bookId]);
            });
        search
            .Setup(service => service.SearchChunksAsync(
                query,
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                receivedBookIds = bookIds;
                return Task.FromResult<IReadOnlyList<RetrievedLibraryChunk>>([]);
            });

        await StreamAsync(agent, messages);

        Assert.Equal(latestUserMessage, receivedMessage);
        Assert.Equal([bookId], receivedBookIds);
    }

    [Fact]
    public async Task StreamAsync_IndependentQuestion_DoesNotReusePriorSourceReferences()
    {
        var priorReference = SourceReference.Create(
            Guid.NewGuid(),
            "A Prior Book",
            4,
            Guid.NewGuid());
        const string latestUserMessage = "Ask an independent question.";
        const string query = "An independent retrieval query.";
        Message[] messages =
        [
            Message.Create(MessageRole.User, "What did the earlier book say?", []),
            Message.Create(MessageRole.Assistant, "Earlier response.", [priorReference]),
            Message.Create(MessageRole.User, latestUserMessage, [])
        ];
        var selection = new ConversationContextSelection(
            [messages[^1]],
            query,
            []);
        var (agent, search, _) = CreateAgent(selection);
        IReadOnlyCollection<Guid>? receivedBookIds = [];
        search
            .Setup(service => service.SearchChunksAsync(
                query,
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                receivedBookIds = bookIds;
                return Task.FromResult<IReadOnlyList<RetrievedLibraryChunk>>([]);
            });

        await StreamAsync(agent, messages);

        Assert.Null(receivedBookIds);
        search.Verify(
            service => service.SearchBooksAsync(
                latestUserMessage,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StreamAsync_FollowUp_CombinesSelectedSourceReferencesAndNewBookMatches()
    {
        var priorBookId = Guid.NewGuid();
        var newBookId = Guid.NewGuid();
        const string latestUserMessage = "Can you explain that further?";
        const string query = "Explain the prior book's theme further.";
        var priorReference = SourceReference.Create(
            priorBookId,
            "The Prior Book",
            9,
            Guid.NewGuid());
        Message[] messages =
        [
            Message.Create(MessageRole.User, "What does this book say about its theme?", []),
            Message.Create(MessageRole.Assistant, "It discusses the theme.", [priorReference]),
            Message.Create(MessageRole.User, latestUserMessage, [])
        ];
        var selection = new ConversationContextSelection(
            messages,
            query,
            [priorReference]);
        var (agent, search, _) = CreateAgent(selection);
        IReadOnlyCollection<Guid>? receivedBookIds = null;
        search
            .Setup(service => service.SearchBooksAsync(
                latestUserMessage,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([newBookId]);
        search
            .Setup(service => service.SearchChunksAsync(
                query,
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                receivedBookIds = bookIds;
                return Task.FromResult<IReadOnlyList<RetrievedLibraryChunk>>([]);
            });

        await StreamAsync(agent, messages);

        Assert.Equal([priorBookId, newBookId], receivedBookIds);
    }

    [Fact]
    public async Task StreamAsync_RetrievedChunks_AreProvidedAsAnswerEvidence()
    {
        var bookId = Guid.NewGuid();
        const string query = "A standalone retrieval query.";
        const string chunkContent = "The book explains the central idea.";
        var sourceReference = SourceReference.Create(
            bookId,
            "The Library Book",
            12,
            Guid.NewGuid());
        Message[] messages = [Message.Create(MessageRole.User, "Question.", [])];
        var selection = new ConversationContextSelection(messages, query, []);
        var (agent, search, client) = CreateAgent(selection);
        IReadOnlyList<ChatMessage>? answerMessages = null;
        search
            .Setup(service => service.SearchChunksAsync(
                query,
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [new RetrievedLibraryChunk(chunkContent, sourceReference)]);
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> chatMessages, ChatOptions? _, CancellationToken _) =>
            {
                answerMessages = chatMessages.ToArray();
                return GetUpdates("Grounded answer.");
            });

        await StreamAsync(agent, messages, bookId);

        var capturedAnswerMessages = answerMessages
            ?? throw new InvalidOperationException("The answer client was not called.");
        var evidenceMessage = Assert.Single(
            capturedAnswerMessages,
            message => message.Text.Contains(chunkContent, StringComparison.Ordinal));
        var instructionMessage = Assert.Single(
            capturedAnswerMessages,
            message => message.Text == LibraryChatInstructions.SystemMessage);
        Assert.Contains(
            "conversation history may clarify the user's intent or references, but it is not factual evidence",
            instructionMessage.Text,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "If the request is unrelated to books or their contents, politely decline to answer.",
            instructionMessage.Text);
        Assert.Contains(
            "the available text from the book does not provide enough information",
            instructionMessage.Text);
        Assert.DoesNotContain("search_books", instructionMessage.Text);
        Assert.DoesNotContain("search_chunks", instructionMessage.Text);
        var evidenceText = evidenceMessage.Text
            ?? throw new InvalidOperationException("The evidence message has no text.");
        Assert.Contains(bookId.ToString(), evidenceText);
        Assert.Contains("The Library Book", evidenceText);
        Assert.Contains(
            (sourceReference.ChunkId ?? throw new InvalidOperationException("The source has no chunk ID."))
            .ToString(),
            evidenceText);
    }

    [Fact]
    public async Task StreamAsync_UnrelatedQuestion_ProvidesDeclineGuidanceAfterRetrieval()
    {
        const string latestUserMessage = "What is the weather today?";
        const string query = "What is the weather today?";
        Message[] messages = [Message.Create(MessageRole.User, latestUserMessage, [])];
        var selection = new ConversationContextSelection(messages, query, []);
        var (agent, search, client) = CreateAgent(selection);
        IReadOnlyCollection<Guid>? receivedBookIds = [];
        IReadOnlyList<ChatMessage>? answerMessages = null;
        var callOrder = new List<string>();
        search
            .Setup(service => service.SearchChunksAsync(
                query,
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                callOrder.Add("chunks");
                receivedBookIds = bookIds;
                return Task.FromResult<IReadOnlyList<RetrievedLibraryChunk>>([]);
            });
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> chatMessages, ChatOptions? _, CancellationToken _) =>
            {
                callOrder.Add("answer");
                answerMessages = chatMessages.ToArray();
                return GetUpdates("Mocked answer.");
            });

        await StreamAsync(agent, messages);

        Assert.Equal(["chunks", "answer"], callOrder);
        Assert.Null(receivedBookIds);
        var capturedAnswerMessages = answerMessages
            ?? throw new InvalidOperationException("The answer client was not called.");
        var instructionMessage = Assert.Single(
            capturedAnswerMessages,
            message => message.Text == LibraryChatInstructions.SystemMessage);
        Assert.Contains(
            "If the request is unrelated to books or their contents, politely decline to answer.",
            instructionMessage.Text);
        Assert.Contains(
            capturedAnswerMessages,
            message => message.Text.Contains("No document chunks were retrieved.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StreamAsync_UnsupportedInBookQuestion_ProvidesInsufficientEvidenceGuidance()
    {
        var requestedBookId = Guid.NewGuid();
        const string latestUserMessage = "What color is the protagonist's coat?";
        const string query = "The protagonist's coat color.";
        Message[] messages = [Message.Create(MessageRole.User, latestUserMessage, [])];
        var selection = new ConversationContextSelection(messages, query, []);
        var (agent, search, client) = CreateAgent(selection);
        IReadOnlyCollection<Guid>? receivedBookIds = null;
        IReadOnlyList<ChatMessage>? answerMessages = null;
        var callOrder = new List<string>();
        search
            .Setup(service => service.SearchChunksAsync(
                query,
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                callOrder.Add("chunks");
                receivedBookIds = bookIds;
                return Task.FromResult<IReadOnlyList<RetrievedLibraryChunk>>([]);
            });
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> chatMessages, ChatOptions? _, CancellationToken _) =>
            {
                callOrder.Add("answer");
                answerMessages = chatMessages.ToArray();
                return GetUpdates("Mocked answer.");
            });

        await StreamAsync(agent, messages, requestedBookId);

        Assert.Equal(["chunks", "answer"], callOrder);
        Assert.Equal([requestedBookId], receivedBookIds);
        search.Verify(
            service => service.SearchBooksAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        var instructionMessage = Assert.Single(
            answerMessages!,
            message => message.Text == LibraryChatInstructions.SystemMessage);
        Assert.Contains(
            "the available text from the book does not provide enough information",
            instructionMessage.Text);
        Assert.Contains(
            answerMessages!,
            message => message.Text.Contains("No document chunks were retrieved.", StringComparison.Ordinal));
    }

    private static (
        LibraryChatAgent Agent,
        Mock<ILibraryChatSearch> Search,
        Mock<IChatClient> Client) CreateAgent(
        ConversationContextSelection selection)
    {
        var contextSelector = new Mock<IConversationContextSelector>();
        contextSelector
            .Setup(service => service.SelectAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(selection);

        var search = new Mock<ILibraryChatSearch>();
        search
            .Setup(service => service.SearchBooksAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        search
            .Setup(service => service.SearchChunksAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RetrievedLibraryChunk>());

        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken _) =>
                GetUpdates("Answer."));

        return (
            new LibraryChatAgent(client.Object, search.Object, contextSelector.Object),
            search,
            client);
    }

    private static async Task<List<LibraryChatAgentUpdate>> StreamAsync(
        LibraryChatAgent agent,
        IReadOnlyList<Message> messages,
        Guid? requestedBookId = null)
    {
        var updates = new List<LibraryChatAgentUpdate>();
        await foreach (var update in agent.StreamAsync(
                           messages,
                           requestedBookId,
                           CancellationToken.None))
        {
            updates.Add(update);
        }

        return updates;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> GetUpdates(params string[] deltas)
    {
        foreach (var delta in deltas)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, delta);
            await Task.Yield();
        }
    }
}
