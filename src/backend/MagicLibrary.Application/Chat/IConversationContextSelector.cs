using MagicLibrary.Core.ValueObjects;

namespace MagicLibrary.Application.Chat;

public interface IConversationContextSelector
{
    Task<ConversationContextSelection> SelectAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken);
}

public sealed record ConversationContextSelection(
    IReadOnlyList<Message> Messages,
    string RetrievalQuery,
    IReadOnlyList<SourceReference> PriorSourceReferences);
