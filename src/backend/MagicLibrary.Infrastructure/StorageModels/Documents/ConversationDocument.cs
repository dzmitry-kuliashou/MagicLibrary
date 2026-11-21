using MagicLibrary.Core.ValueObjects;

namespace MagicLibrary.Infrastructure.StorageModels.Documents;

public sealed class ConversationDocument : StorageDocument
{
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<ConversationMessageDocument> Messages { get; set; } = [];
}

public sealed class ConversationMessageDocument
{
    public MessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public List<ConversationSourceReferenceDocument> SourceReferences { get; set; } = [];
}

public sealed class ConversationSourceReferenceDocument
{
    public Guid BookId { get; set; }

    public string BookTitle { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public Guid? ChunkId { get; set; }
}
