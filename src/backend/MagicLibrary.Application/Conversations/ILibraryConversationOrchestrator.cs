using ErrorOr;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.Commands;

namespace MagicLibrary.Application.Conversations;

public interface ILibraryConversationOrchestrator
{
    Task<ErrorOr<IAsyncEnumerable<ChatStreamEvent>>> StreamAsync(
        LibraryChatCommand command,
        CancellationToken cancellationToken);
}
