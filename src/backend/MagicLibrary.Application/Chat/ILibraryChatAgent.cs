using MagicLibrary.Core.ValueObjects;

namespace MagicLibrary.Application.Chat;

public interface ILibraryChatAgent
{
    IAsyncEnumerable<LibraryChatAgentUpdate> StreamAsync(
        IReadOnlyList<Message> messages,
        Guid? requestedBookId,
        CancellationToken cancellationToken);
}

public sealed record LibraryChatAgentUpdate(
    string? Text,
    IReadOnlyList<SourceReference>? SourceReferences = null);
