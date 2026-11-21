using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories.Documents;

internal sealed class ConversationWriteRepository(IDocumentSession documentSession)
    : DocumentWriteRepository<Conversation, ConversationDocument>(documentSession), IConversationWriteRepository
{
    protected override ConversationDocument ToDocument(Conversation aggregate) =>
        ConversationDocumentMapper.ToDocument(aggregate);
}
