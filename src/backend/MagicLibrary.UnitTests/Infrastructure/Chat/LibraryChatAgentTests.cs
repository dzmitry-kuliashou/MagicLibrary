using System.Text.Json;
using MagicLibrary.Application.Chat;
using MagicLibrary.Core.ValueObjects;
using MagicLibrary.Infrastructure.Chat;
using MagicLibrary.Infrastructure.KeywordExtraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Infrastructure.Chat;

public sealed class LibraryChatAgentTests
{
    [Fact]
    public async Task StreamAsync_ExplicitBookId_UsesRequestedBookAndSkipsBookSearch()
    {
        var requestedBookId = Guid.NewGuid();
        var modelSelectedBookId = Guid.NewGuid();
        const string query = "A standalone retrieval query.";
        var sourceReference = SourceReference.Create(
            requestedBookId,
            "The Library Book",
            12,
            Guid.NewGuid());
        IReadOnlyCollection<Guid>? receivedBookIds = null;
        ChatOptions? receivedOptions = null;
        var responseCount = 0;
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? options, CancellationToken _) =>
            {
                receivedOptions = options;
                return responseCount++ == 0
                    ? GetFunctionCallUpdate(
                        "search-chunks-1",
                        "search_chunks",
                        new Dictionary<string, object?>
                        {
                            ["query"] = query,
                            ["bookIds"] = new[] { modelSelectedBookId }
                        })
                    : GetUpdates("Grounded answer.");
            });
        var tools = new Mock<ILibraryChatTools>();
        tools
            .Setup(service => service.SearchChunksAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                receivedBookIds = bookIds;
                return Task.FromResult<IReadOnlyList<RetrievedLibraryChunk>>(
                    [new RetrievedLibraryChunk("Evidence.", sourceReference)]);
            });
        var agent = new LibraryChatAgent(
            client.Object,
            tools.Object,
            Options.Create(new KeywordExtractionOptions()));
        Message[] messages =
        [
            Message.Create(MessageRole.User, "Question.", [])
        ];
        var updates = new List<LibraryChatAgentUpdate>();

        await foreach (var update in agent.StreamAsync(
                           messages,
                           requestedBookId,
                           CancellationToken.None))
        {
            updates.Add(update);
        }

        var registeredTools = receivedOptions?.Tools
            ?? throw new InvalidOperationException("The agent did not register its tools.");
        Assert.Equal(["search_chunks"], registeredTools.Select(tool => tool.Name));
        Assert.Equal([requestedBookId], receivedBookIds);
        Assert.Equal(
            [sourceReference],
            Assert.Single(updates, update => update.SourceReferences is not null).SourceReferences);
        tools.Verify(
            service => service.SearchBooksAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(2, responseCount);
    }

    [Fact]
    public async Task StreamAsync_NoMatchingBooks_SearchesEntireLibrary()
    {
        const string query = "A standalone retrieval query.";
        const string chunkContent = "Relevant library-wide evidence.";
        var sourceReference = SourceReference.Create(
            Guid.NewGuid(),
            "A Library Book",
            8,
            Guid.NewGuid());
        IReadOnlyCollection<Guid>? receivedBookIds = [];
        var responseCount = 0;
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken _) =>
            {
                var responseIndex = responseCount++;
                return responseIndex switch
                {
                    0 => GetFunctionCallUpdate(
                        "search-books-1",
                        "search_books",
                        new Dictionary<string, object?> { ["terms"] = new[] { "unmatched title" } }),
                    1 => GetFunctionCallUpdate(
                        "search-chunks-1",
                        "search_chunks",
                        new Dictionary<string, object?>
                        {
                            ["query"] = query,
                            ["bookIds"] = Array.Empty<Guid>()
                        }),
                    _ => GetUpdates("Answer from the library.")
                };
            });
        var tools = new Mock<ILibraryChatTools>();
        tools
            .Setup(service => service.SearchBooksAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<IReadOnlyList<Guid>>([]));
        IReadOnlyList<RetrievedLibraryChunk> chunks =
            [new RetrievedLibraryChunk(chunkContent, sourceReference)];
        tools
            .Setup(service => service.SearchChunksAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                receivedBookIds = bookIds;
                return Task.FromResult(chunks);
            });
        var agent = new LibraryChatAgent(
            client.Object,
            tools.Object,
            Options.Create(new KeywordExtractionOptions()));
        Message[] messages =
        [
            Message.Create(MessageRole.User, "Find books about an unmatched title.", [])
        ];
        var updates = new List<LibraryChatAgentUpdate>();

        await foreach (var update in agent.StreamAsync(messages, null, CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Null(receivedBookIds);
        Assert.Equal(
            [sourceReference],
            Assert.Single(updates, update => update.SourceReferences is not null).SourceReferences);
        Assert.Equal(3, responseCount);
    }

    [Fact]
    public async Task StreamAsync_ScriptedUnscopedSearch_IgnoresPriorSourceReferences()
    {
        var priorBookId = Guid.NewGuid();
        var priorSourceReference = SourceReference.Create(
            priorBookId,
            "A Prior Book",
            3,
            Guid.NewGuid());
        const string query = "A standalone whole-library retrieval query.";
        var retrievedSourceReference = SourceReference.Create(
            Guid.NewGuid(),
            "Another Library Book",
            8,
            Guid.NewGuid());
        IReadOnlyCollection<Guid>? receivedBookIds = [priorBookId];
        var responseCount = 0;
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken _) =>
                responseCount++ == 0
                    ? GetFunctionCallUpdate(
                        "search-chunks-1",
                        "search_chunks",
                        new Dictionary<string, object?>
                        {
                            ["query"] = query,
                            ["bookIds"] = Array.Empty<Guid>()
                        })
                    : GetUpdates("Answer from the whole library."));
        var tools = new Mock<ILibraryChatTools>();
        tools
            .Setup(service => service.SearchChunksAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                receivedBookIds = bookIds;
                return Task.FromResult<IReadOnlyList<RetrievedLibraryChunk>>(
                    [new RetrievedLibraryChunk("Whole-library evidence.", retrievedSourceReference)]);
            });
        var agent = new LibraryChatAgent(
            client.Object,
            tools.Object,
            Options.Create(new KeywordExtractionOptions()));
        Message[] messages =
        [
            Message.Create(MessageRole.User, "What did the book say?", []),
            Message.Create(MessageRole.Assistant, "I found a book about this earlier.", [priorSourceReference]),
            Message.Create(MessageRole.User, "Search across the whole library.", [])
        ];
        var updates = new List<LibraryChatAgentUpdate>();

        await foreach (var update in agent.StreamAsync(messages, null, CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Null(receivedBookIds);
        Assert.Equal(
            [retrievedSourceReference],
            Assert.Single(updates, update => update.SourceReferences is not null).SourceReferences);
        Assert.Equal(2, responseCount);
    }

    [Fact]
    public async Task StreamAsync_PriorAssistantSourceReferences_AreMappedAndAcceptedForScopedRetrieval()
    {
        var bookId = Guid.NewGuid();
        const string query = "The standalone follow-up retrieval query.";
        var priorSourceReference = SourceReference.Create(
            bookId,
            "The Library Book",
            12,
            Guid.NewGuid());
        var retrievedSourceReference = SourceReference.Create(
            bookId,
            "The Library Book",
            24,
            Guid.NewGuid());
        IReadOnlyCollection<Guid>? receivedBookIds = null;
        IReadOnlyList<ChatMessage>? firstRequestMessages = null;
        var responseCount = 0;
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
            {
                var responseIndex = responseCount++;
                if (responseIndex == 0)
                {
                    firstRequestMessages = messages.ToArray();
                }

                return responseIndex == 0
                    ? GetFunctionCallUpdate(
                        "search-chunks-1",
                        "search_chunks",
                        new Dictionary<string, object?>
                        {
                            ["query"] = query,
                            ["bookIds"] = new[] { bookId }
                        })
                    : GetUpdates("Follow-up answer.");
            });
        var tools = new Mock<ILibraryChatTools>();
        tools
            .Setup(service => service.SearchChunksAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                receivedBookIds = bookIds;
                return Task.FromResult<IReadOnlyList<RetrievedLibraryChunk>>(
                    [new RetrievedLibraryChunk("Follow-up evidence.", retrievedSourceReference)]);
            });
        var agent = new LibraryChatAgent(
            client.Object,
            tools.Object,
            Options.Create(new KeywordExtractionOptions()));
        Message[] history =
        [
            Message.Create(MessageRole.System, "Outdated system instructions.", []),
            Message.Create(MessageRole.User, "What does this book say about its theme?", []),
            Message.Create(MessageRole.Assistant, "It discusses the theme.", [priorSourceReference]),
            Message.Create(MessageRole.User, "Can you explain that further?", [])
        ];
        var updates = new List<LibraryChatAgentUpdate>();

        await foreach (var update in agent.StreamAsync(history, null, CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Equal([bookId], receivedBookIds);
        var mappedAssistantMessage = Assert.Single(
            firstRequestMessages!,
            message => message.Role == ChatRole.Assistant);
        Assert.Contains(priorSourceReference.BookId.ToString(), mappedAssistantMessage.Text);
        tools.Verify(
            service => service.SearchBooksAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(
            [retrievedSourceReference],
            Assert.Single(updates, update => update.SourceReferences is not null).SourceReferences);
    }

    [Fact]
    public async Task StreamAsync_ProcessesBookSearchAndChunkCallsBeforeStreamingAnswer()
    {
        var bookId = Guid.NewGuid();
        var searchTerms = new[] { "author name", "keyword" };
        const string retrievalQuery = "author name main idea";
        const string chunkContent = "The book explains the main idea.";
        const string answer = "The book explains the main idea.";
        var sourceReference = SourceReference.Create(bookId, "The Library Book", 12, Guid.NewGuid());
        IReadOnlyList<RetrievedLibraryChunk> chunks =
            [new RetrievedLibraryChunk(chunkContent, sourceReference)];
        var toolCalls = new List<string>();
        IReadOnlyList<string>? receivedTerms = null;
        string? receivedQuery = null;
        IReadOnlyCollection<Guid>? receivedBookIds = null;
        IReadOnlyList<ChatMessage>? finalRequestMessages = null;
        var responseCount = 0;
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
            {
                var responseIndex = responseCount++;
                if (responseIndex == 2)
                {
                    finalRequestMessages = messages.ToArray();
                }

                return responseIndex switch
                {
                    0 => GetFunctionCallUpdate(
                        "search-books-1",
                        "search_books",
                        new Dictionary<string, object?> { ["terms"] = searchTerms }),
                    1 => GetFunctionCallUpdate(
                        "search-chunks-1",
                        "search_chunks",
                        new Dictionary<string, object?>
                        {
                            ["query"] = retrievalQuery,
                            ["bookIds"] = new[] { bookId }
                        }),
                    _ => GetUpdates(answer)
                };
            });
        var tools = new Mock<ILibraryChatTools>();
        tools
            .Setup(service => service.SearchBooksAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<string> terms, CancellationToken _) =>
            {
                receivedTerms = terms;
                toolCalls.Add("search_books");
                return Task.FromResult<IReadOnlyList<Guid>>([bookId]);
            });
        tools
            .Setup(service => service.SearchChunksAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string query, IReadOnlyCollection<Guid>? bookIds, CancellationToken _) =>
            {
                receivedQuery = query;
                receivedBookIds = bookIds;
                toolCalls.Add("search_chunks");
                return Task.FromResult(chunks);
            });
        var agent = new LibraryChatAgent(
            client.Object,
            tools.Object,
            Options.Create(new KeywordExtractionOptions()));
        Message[] messages =
        [
            Message.Create(MessageRole.System, "Outdated system instructions.", []),
            Message.Create(MessageRole.User, "Find the author's book and explain its main idea.", [])
        ];
        var updates = new List<LibraryChatAgentUpdate>();

        await foreach (var update in agent.StreamAsync(messages, null, CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Equal(["search_books", "search_chunks"], toolCalls);
        Assert.Equal(searchTerms, receivedTerms);
        Assert.Equal(retrievalQuery, receivedQuery);
        Assert.Equal([bookId], receivedBookIds);
        Assert.Equal([answer], updates.Where(update => update.Text is not null).Select(update => update.Text));
        Assert.Equal(
            [sourceReference],
            Assert.Single(updates, update => update.SourceReferences is not null).SourceReferences);
        var functionResults = finalRequestMessages!
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => JsonSerializer.Serialize(result.Result));
        Assert.Contains(functionResults, result => result.Contains(chunkContent, StringComparison.Ordinal));
        Assert.Equal(3, responseCount);
    }

    [Fact]
    public async Task StreamAsync_ScriptedEmptyChunkResult_ProvidesInstructionWithoutSourceReferences()
    {
        const string query = "A standalone retrieval query.";
        IReadOnlyList<ChatMessage>? initialRequestMessages = null;
        IReadOnlyList<ChatMessage>? finalRequestMessages = null;
        var responseCount = 0;
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
            {
                var responseIndex = responseCount++;
                if (responseIndex == 0)
                {
                    initialRequestMessages = messages.ToArray();
                }
                else
                {
                    finalRequestMessages = messages.ToArray();
                }

                return responseIndex == 0
                    ? GetFunctionCallUpdate(
                        "search-chunks-1",
                        "search_chunks",
                        new Dictionary<string, object?>
                        {
                            ["query"] = query,
                            ["bookIds"] = Array.Empty<Guid>()
                        })
                    : GetUpdates("Scripted response.");
            });
        var tools = new Mock<ILibraryChatTools>();
        tools
            .Setup(service => service.SearchChunksAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<IReadOnlyList<RetrievedLibraryChunk>>([]));
        var agent = new LibraryChatAgent(
            client.Object,
            tools.Object,
            Options.Create(new KeywordExtractionOptions()));
        Message[] messages =
        [
            Message.Create(MessageRole.User, "What information does the library provide?", [])
        ];
        var updates = new List<LibraryChatAgentUpdate>();

        await foreach (var update in agent.StreamAsync(messages, null, CancellationToken.None))
        {
            updates.Add(update);
        }

        var systemMessage = Assert.Single(
            initialRequestMessages!,
            message => message.Role == ChatRole.System);
        Assert.Contains(
            "If no relevant chunks are retrieved, explain that the library does not provide enough information.",
            systemMessage.Text);
        var functionResults = finalRequestMessages!
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => JsonSerializer.Serialize(result.Result));
        Assert.Contains(functionResults, result => result == "[]");
        Assert.Empty(
            Assert.Single(updates, update => update.SourceReferences is not null).SourceReferences!);
        Assert.Equal(2, responseCount);
    }

    [Fact]
    public async Task StreamAsync_MapsHistoryAndRegistersRetrievalTools()
    {
        IReadOnlyList<ChatMessage>? receivedMessages = null;
        ChatOptions? receivedOptions = null;
        var (agent, client) = CreateAgent(
            (messages, options, _) =>
            {
                receivedMessages = messages.ToArray();
                receivedOptions = options;
                return GetUpdates("The ", "", "answer.");
            });
        var sourceReference = SourceReference.Create(
            Guid.NewGuid(),
            "The Library Book",
            12,
            Guid.NewGuid());
        Message[] history =
        [
            Message.Create(MessageRole.System, "Outdated system instructions.", []),
            Message.Create(MessageRole.User, "Earlier question.", []),
            Message.Create(MessageRole.Assistant, "Earlier answer.", [sourceReference]),
            Message.Create(MessageRole.User, "Current question.", [])
        ];
        var updates = new List<LibraryChatAgentUpdate>();

        await foreach (var update in agent.StreamAsync(history, null, CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Collection(
            receivedMessages!,
            systemMessage =>
            {
                Assert.Equal(ChatRole.System, systemMessage.Role);
                Assert.Equal(LibraryChatInstructions.SystemMessage, systemMessage.Text);
            },
            userMessage => Assert.Equal("Earlier question.", userMessage.Text),
            assistantMessage =>
            {
                Assert.Equal(ChatRole.Assistant, assistantMessage.Role);
                Assert.Contains(sourceReference.BookId.ToString(), assistantMessage.Text);
                Assert.Contains(sourceReference.BookTitle, assistantMessage.Text);
            },
            userMessage => Assert.Equal("Current question.", userMessage.Text));
        var tools = receivedOptions?.Tools
            ?? throw new InvalidOperationException("The agent did not register its tools.");
        Assert.Equal(["search_books", "search_chunks"], tools.Select(tool => tool.Name));
        var searchBooksTool = Assert.Single(tools, tool => tool.Name == "search_books");
        Assert.Contains(
            $"up to {new KeywordExtractionOptions().MaxResults}",
            searchBooksTool.Description);
        Assert.Contains(
            $"no more than {KeywordExtractionOptions.MaximumPhraseWords} words",
            searchBooksTool.Description);
        var searchChunksTool = Assert.Single(tools, tool => tool.Name == "search_chunks");
        Assert.Contains("Content is the evidence text", searchChunksTool.Description);
        Assert.Contains("SourceReference contains citation metadata only", searchChunksTool.Description);
        Assert.Equal(["The ", "answer."], updates.Where(update => update.Text is not null).Select(update => update.Text));
        Assert.Empty(Assert.Single(updates, update => update.SourceReferences is not null).SourceReferences!);
        client.Verify(
            service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                CancellationToken.None),
            Times.Once);
    }

    private static (
        LibraryChatAgent Agent,
        Mock<IChatClient> Client) CreateAgent(
        Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken,
            IAsyncEnumerable<ChatResponseUpdate>> updateFactory)
    {
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? options,
                    CancellationToken cancellationToken) =>
                updateFactory(messages, options, cancellationToken));

        return (
            new LibraryChatAgent(
                client.Object,
                Mock.Of<ILibraryChatTools>(),
                Options.Create(new KeywordExtractionOptions())),
            client);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> GetUpdates(params string[] deltas)
    {
        foreach (var delta in deltas)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, delta);
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> GetFunctionCallUpdate(
        string callId,
        string functionName,
        IDictionary<string, object?> arguments)
    {
        yield return new ChatResponseUpdate(
            ChatRole.Assistant,
            [new FunctionCallContent(callId, functionName, arguments)]);
        await Task.Yield();
    }
}
