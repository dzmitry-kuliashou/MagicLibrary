using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Core.Repositories.Documents;

public interface IConversationReadRepository : IDocumentReadRepository<Conversation>
{
    Task<IReadOnlyList<Conversation>> ListLibraryConversationsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Conversation>> ListBookConversationsAsync(
        Guid bookId,
        CancellationToken cancellationToken);
}
