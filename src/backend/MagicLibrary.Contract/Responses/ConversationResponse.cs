namespace MagicLibrary.Contract.Responses;

public sealed record ConversationResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IEnumerable<ConversationMessageResponse> Messages);
