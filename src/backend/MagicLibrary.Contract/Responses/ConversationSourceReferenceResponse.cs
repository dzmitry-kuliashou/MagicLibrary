namespace MagicLibrary.Contract.Responses;

public sealed record ConversationSourceReferenceResponse(
    Guid BookId,
    string BookTitle,
    int PageNumber,
    Guid? ChunkId);
