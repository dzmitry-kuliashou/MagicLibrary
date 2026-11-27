namespace MagicLibrary.Application.Commands;

public sealed record LibraryChatCommand(
    Guid? ConversationId,
    string Message,
    Guid? BookId = null);
