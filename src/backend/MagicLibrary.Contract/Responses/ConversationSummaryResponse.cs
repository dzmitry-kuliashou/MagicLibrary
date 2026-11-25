namespace MagicLibrary.Contract.Responses;

public sealed record ConversationSummaryResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
