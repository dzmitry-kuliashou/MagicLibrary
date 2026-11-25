namespace MagicLibrary.Contract.Responses;

public sealed record ConversationMessageResponse(
    string Role,
    string Content,
    IEnumerable<ConversationSourceReferenceResponse> SourceReferences);
