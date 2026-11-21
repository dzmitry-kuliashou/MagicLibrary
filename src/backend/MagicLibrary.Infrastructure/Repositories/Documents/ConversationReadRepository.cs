using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories.Documents;

internal sealed class ConversationReadRepository(IQuerySession querySession)
    : DocumentReadRepository<Conversation, ConversationDocument>(querySession), IConversationReadRepository
{
    protected override Conversation ToAggregate(ConversationDocument document) =>
        ConversationDocumentMapper.ToAggregate(document);
}
