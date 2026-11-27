using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MagicLibrary.Application.Chat;
using MagicLibrary.Core.ValueObjects;
using MagicLibrary.Infrastructure.KeywordExtraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MagicLibrary.Infrastructure.Chat;

public sealed class LibraryChatAgent(
    IChatClient chatClient,
    ILibraryChatTools libraryChatTools,
    IOptions<KeywordExtractionOptions> keywordExtractionOptions) : ILibraryChatAgent
{
    private readonly IChatClient _functionInvokingChatClient = chatClient
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();

    public async IAsyncEnumerable<LibraryChatAgentUpdate> StreamAsync(
        IReadOnlyList<Message> messages,
        Guid? requestedBookId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var allowedBookIds = new ConcurrentDictionary<Guid, byte>(
            messages
                .Where(message => message.Role == MessageRole.Assistant)
                .SelectMany(message => message.SourceReferences)
                .Select(sourceReference => sourceReference.BookId)
                .Distinct()
                .Select(bookId => new KeyValuePair<Guid, byte>(bookId, 0)));
        var sourceReferences = new ConcurrentDictionary<SourceReference, byte>();

        async Task<IReadOnlyList<Guid>> SearchBooksAsync(
            IReadOnlyList<string> terms,
            CancellationToken functionCancellationToken)
        {
            var matches = await libraryChatTools.SearchBooksAsync(terms, functionCancellationToken);
            foreach (var bookId in matches)
            {
                allowedBookIds.TryAdd(bookId, 0);
            }

            return matches;
        }

        async Task<IReadOnlyList<RetrievedLibraryChunk>> SearchChunksAsync(
            string query,
            IReadOnlyList<Guid>? bookIds,
            CancellationToken functionCancellationToken)
        {
            IReadOnlyCollection<Guid>? effectiveBookIds;
            if (requestedBookId is { } explicitBookId)
            {
                effectiveBookIds = [explicitBookId];
            }
            else if (bookIds is null || bookIds.Count == 0)
            {
                effectiveBookIds = null;
            }
            else
            {
                var validatedBookIds = bookIds
                    .Where(allowedBookIds.ContainsKey)
                    .Distinct()
                    .ToArray();
                effectiveBookIds = validatedBookIds.Length > 0
                    ? validatedBookIds
                    : null;
            }

            var chunks = await libraryChatTools.SearchChunksAsync(
                query,
                effectiveBookIds,
                functionCancellationToken);
            foreach (var sourceReference in chunks
                         .Select(chunk => chunk.SourceReference)
                         .OfType<SourceReference>())
            {
                sourceReferences.TryAdd(sourceReference, 0);
            }

            return chunks;
        }

        var maxKeywordResults = keywordExtractionOptions.Value.MaxResults;
        var searchBooksFunction = AIFunctionFactory.Create(
            (Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<Guid>>>)SearchBooksAsync,
            "search_books",
            $"Search books by title, author, or keyword. Preserve exact title and author terms when applicable, plus up to {maxKeywordResults} " +
            $"keyword phrases of no more than {KeywordExtractionOptions.MaximumPhraseWords} words each, ordered by relevance. Return matching book IDs.");
        var searchChunksFunction = AIFunctionFactory.Create(
            (Func<string, IReadOnlyList<Guid>?, CancellationToken,
                Task<IReadOnlyList<RetrievedLibraryChunk>>>)SearchChunksAsync,
            "search_chunks",
            "Retrieve relevant document chunks using a standalone query and optional book IDs. Use null or an empty list for the whole library. " +
            "Each result's Content is the evidence text; SourceReference contains citation metadata only.");
        var chatOptions = new ChatOptions
        {
            Tools = requestedBookId.HasValue
                ? [searchChunksFunction]
                : [searchBooksFunction, searchChunksFunction]
        };

        await foreach (var update in _functionInvokingChatClient.GetStreamingResponseAsync(
                           MapRetrievalMessages(messages),
                           chatOptions,
                           cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return new LibraryChatAgentUpdate(update.Text);
            }
        }

        yield return new LibraryChatAgentUpdate(null, sourceReferences.Keys.ToArray());
    }

    private static ChatMessage[] MapRetrievalMessages(IReadOnlyList<Message> messages)
    {
        var mappedMessages = new List<ChatMessage>
        {
            new(ChatRole.System, LibraryChatInstructions.SystemMessage)
        };

        mappedMessages.AddRange(messages
            .Where(message => message.Role != MessageRole.System)
            .Select(message =>
            {
                var content = message.Content;
                if (message.Role == MessageRole.Assistant && message.SourceReferences.Count > 0)
                {
                    var references = string.Join(
                        Environment.NewLine,
                        message.SourceReferences.Select(sourceReference =>
                            $"bookId={sourceReference.BookId}; title={sourceReference.BookTitle}; " +
                            $"page={sourceReference.PageNumber}; chunkId={sourceReference.ChunkId}"));
                    content += $"{Environment.NewLine}{Environment.NewLine}Prior source references (metadata):{Environment.NewLine}{references}";
                }

                return new ChatMessage(MapRole(message.Role), content);
            }));

        return mappedMessages.ToArray();
    }

    private static ChatRole MapRole(MessageRole role) =>
        role switch
        {
            MessageRole.System => ChatRole.System,
            MessageRole.User => ChatRole.User,
            MessageRole.Assistant => ChatRole.Assistant,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
}
