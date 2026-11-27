namespace MagicLibrary.Application.Conversations;

public abstract record ChatStreamEvent
{
    private ChatStreamEvent()
    {
    }

    public sealed record Status(Guid ConversationId) : ChatStreamEvent;

    public sealed record Text(string Delta) : ChatStreamEvent;

    public sealed record Completed : ChatStreamEvent;

    public sealed record Error(string Message) : ChatStreamEvent;
}
