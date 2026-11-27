using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.ValueObjects;
using MagicLibrary.Infrastructure.StorageModels.Documents;

namespace MagicLibrary.Infrastructure.StorageModels.Documents.Mappers;

internal static class ConversationDocumentMapper
{
    internal static ConversationDocument ToDocument(Conversation conversation)
    {
        return new ConversationDocument
        {
            Id = conversation.Id,
            Name = conversation.Name,
            BookId = conversation.BookId,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            Messages = conversation.Messages
                .Select(message => new ConversationMessageDocument
                {
                    Role = message.Role,
                    Status = message.Status,
                    Content = message.Content,
                    SourceReferences = message.SourceReferences
                        .Select(sourceReference => new ConversationSourceReferenceDocument
                        {
                            BookId = sourceReference.BookId,
                            BookTitle = sourceReference.BookTitle,
                            PageNumber = sourceReference.PageNumber,
                            ChunkId = sourceReference.ChunkId
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    internal static Conversation ToAggregate(ConversationDocument document)
    {
        var messages = document.Messages
            .Select(message => Message.Create(
                message.Role,
                message.Content,
                message.SourceReferences.Select(sourceReference => SourceReference.Create(
                    sourceReference.BookId,
                    sourceReference.BookTitle,
                    sourceReference.PageNumber,
                    sourceReference.ChunkId)),
                message.Status)
            )
            .ToList();

        return Conversation.Reconstitute(
            document.Id,
            document.Name,
            document.BookId,
            document.CreatedAt,
            document.UpdatedAt,
            messages);
    }
}
