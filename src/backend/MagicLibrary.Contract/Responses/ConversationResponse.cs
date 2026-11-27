namespace MagicLibrary.Contract.Responses;

public sealed record ConversationResponse(
    Guid Id,
    string Name,
    Guid? BookId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IEnumerable<ConversationMessageResponse> Messages)
{
    public bool IsLastMessagePending => Messages.LastOrDefault()?.Status == "Pending";
}
