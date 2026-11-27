namespace MagicLibrary.Contract.Requests;

public sealed record LibraryChatRequest(
    Guid? ConversationId,
    string Message,
    Guid? BookId = null);
