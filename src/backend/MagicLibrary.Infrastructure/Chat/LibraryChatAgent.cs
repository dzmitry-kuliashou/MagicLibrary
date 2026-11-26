using System.Runtime.CompilerServices;
using MagicLibrary.Application.Chat;
using MagicLibrary.Core.ValueObjects;
using Microsoft.Extensions.AI;

namespace MagicLibrary.Infrastructure.Chat;

public sealed class LibraryChatAgent(IChatClient chatClient) : ILibraryChatAgent
{
    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<Message> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var chatMessages = messages
            .Select(message => new ChatMessage(MapRole(message.Role), message.Content))
            .ToArray();

        await foreach (var update in chatClient
                           .GetStreamingResponseAsync(chatMessages, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
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
