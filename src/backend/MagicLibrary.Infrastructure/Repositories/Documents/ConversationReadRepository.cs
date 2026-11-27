using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories.Documents;

internal sealed class ConversationReadRepository(IQuerySession querySession)
    : DocumentReadRepository<Conversation, ConversationDocument>(querySession), IConversationReadRepository
{
    private readonly IQuerySession _querySession = querySession;

    protected override Conversation ToAggregate(ConversationDocument document) =>
        ConversationDocumentMapper.ToAggregate(document);

    public async Task<IReadOnlyList<Conversation>> ListLibraryConversationsAsync(
        CancellationToken cancellationToken)
    {
        var documents = await _querySession.Query<ConversationDocument>()
            .Where(document => document.BookId == null)
            .OrderByDescending(document => document.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        return documents.Select(ToAggregate).ToArray();
    }

    public async Task<IReadOnlyList<Conversation>> ListBookConversationsAsync(
        Guid bookId,
        CancellationToken cancellationToken)
    {
        var documents = await _querySession.Query<ConversationDocument>()
            .Where(document => document.BookId == bookId)
            .OrderByDescending(document => document.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        return documents.Select(ToAggregate).ToArray();
    }
}
