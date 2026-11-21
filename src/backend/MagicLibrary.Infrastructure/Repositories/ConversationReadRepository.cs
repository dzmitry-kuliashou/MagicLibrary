using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories;
using MagicLibrary.Infrastructure.StorageModels;
using MagicLibrary.Infrastructure.StorageModels.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories;

internal sealed class ConversationReadRepository(IQuerySession querySession)
    : ReadRepository<Conversation, ConversationDocument>(querySession), IConversationReadRepository
{
    protected override Conversation ToAggregate(ConversationDocument document) =>
        ConversationDocumentMapper.ToAggregate(document);
}
