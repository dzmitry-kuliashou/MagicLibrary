namespace MagicLibrary.Contract.Responses;

public sealed record ConversationMessageResponse(
    string Role,
    string Status,
    string Content,
    IEnumerable<ConversationSourceReferenceResponse> SourceReferences);
