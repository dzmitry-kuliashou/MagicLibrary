using System.Runtime.CompilerServices;
using MagicLibrary.Application.Chat;
using MagicLibrary.Core.ValueObjects;
using Microsoft.Extensions.AI;

namespace MagicLibrary.Infrastructure.Chat;

public sealed class LibraryChatAgent(
    IChatClient chatClient,
    ILibraryChatSearch libraryChatSearch,
    IConversationContextSelector contextSelector) : ILibraryChatAgent
{
    public async IAsyncEnumerable<LibraryChatAgentUpdate> StreamAsync(
        IReadOnlyList<Message> messages,
        Guid? requestedBookId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var context = await contextSelector.SelectAsync(messages, cancellationToken);
        IReadOnlyCollection<Guid>? bookIds;
        if (requestedBookId is { } explicitBookId)
        {
            bookIds = [explicitBookId];
        }
        else
        {
            var latestUserMessage = context.Messages.Last(message => message.Role == MessageRole.User);
            var bookMatches = await libraryChatSearch.SearchBooksAsync(
                latestUserMessage.Content,
                cancellationToken);
            var selectedBookIds = context.PriorSourceReferences
                .Select(sourceReference => sourceReference.BookId)
                .Concat(bookMatches)
                .Distinct()
                .ToArray();
            bookIds = selectedBookIds.Length > 0 ? selectedBookIds : null;
        }

        var chunks = await libraryChatSearch.SearchChunksAsync(
            context.RetrievalQuery,
            bookIds,
            cancellationToken);
        var sourceReferences = chunks
            .Select(chunk => chunk.SourceReference)
            .OfType<SourceReference>()
            .Distinct()
            .ToArray();

        await foreach (var update in chatClient.GetStreamingResponseAsync(
                           MapAnswerMessages(context.Messages, chunks),
                           null,
                           cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return new LibraryChatAgentUpdate(update.Text);
            }
        }

        yield return new LibraryChatAgentUpdate(null, sourceReferences);
    }

    private static ChatMessage[] MapAnswerMessages(
        IReadOnlyList<Message> messages,
        IReadOnlyList<RetrievedLibraryChunk> chunks)
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

        var evidence = chunks.Count == 0
            ? "No document chunks were retrieved."
            : string.Join(
                Environment.NewLine + Environment.NewLine,
                chunks.Select((chunk, index) =>
                {
                    var sourceReference = chunk.SourceReference is { } reference
                        ? $"bookId={reference.BookId}; title={reference.BookTitle}; " +
                          $"page={reference.PageNumber}; chunkId={reference.ChunkId}"
                        : "unavailable";
                    return $"Chunk {index + 1} content:{Environment.NewLine}{chunk.Content}" +
                           $"{Environment.NewLine}Source reference metadata: {sourceReference}";
                }));
        mappedMessages.Add(new ChatMessage(
            ChatRole.System,
            $"Retrieved document chunks (content is evidence; source references are citation metadata):{Environment.NewLine}{evidence}"));

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
