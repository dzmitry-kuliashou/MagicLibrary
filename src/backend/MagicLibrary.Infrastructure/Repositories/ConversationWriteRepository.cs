using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories;
using MagicLibrary.Infrastructure.StorageModels;
using MagicLibrary.Infrastructure.StorageModels.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories;

internal sealed class ConversationWriteRepository(IDocumentSession documentSession)
    : WriteRepository<Conversation, ConversationDocument>(documentSession), IConversationWriteRepository
{
    protected override ConversationDocument ToDocument(Conversation aggregate) =>
        ConversationDocumentMapper.ToDocument(aggregate);
}
