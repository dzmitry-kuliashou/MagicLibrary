using MagicLibrary.Core.ValueObjects;

namespace MagicLibrary.Application.Chat;

public interface ILibraryChatAgent
{
    IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken);
}
