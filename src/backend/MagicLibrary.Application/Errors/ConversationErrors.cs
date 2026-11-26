using ErrorOr;

namespace MagicLibrary.Application.Errors;

public static class ConversationErrors
{
    public static Error InvalidId =>
        Error.Validation("Conversation.InvalidId", "A non-default conversation ID is required.");

    public static Error MissingName =>
        Error.Validation("Conversation.MissingName", "A conversation name is required.");

    public static Error MissingSourceReferences =>
        Error.Validation("Conversation.MissingSourceReferences", "Source references are required.");

    public static Error MissingContent =>
        Error.Validation("Conversation.MissingContent", "Message content is required.");

    public static Error ConversationNotFound(Guid id) =>
        Error.NotFound("Conversation.NotFound", $"The conversation with ID '{id}' was not found.");

    public static Error AssistantResponsePending =>
        Error.Conflict(
            "Conversation.AssistantResponsePending",
            "A new message cannot be added while an assistant response is pending.");

    public static Error AssistantResponseNotPending =>
        Error.Conflict(
            "Conversation.AssistantResponseNotPending",
            "The assistant response can only be updated while it is pending.");
}
