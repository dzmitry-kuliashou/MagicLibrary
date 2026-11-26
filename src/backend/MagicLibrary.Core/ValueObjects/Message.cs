using Ardalis.GuardClauses;

namespace MagicLibrary.Core.ValueObjects;

public enum MessageRole
{
    System,
    User,
    Assistant
}

public enum MessageStatus
{
    Pending,
    Completed,
    Failed,
    Interrupted
}

public sealed record Message
{
    public static Message Create(
        MessageRole role,
        string content,
        IEnumerable<SourceReference> sourceReferences,
        MessageStatus status = MessageStatus.Completed)
    {
        return new Message(role, content, sourceReferences, status);
    }

    private Message(
        MessageRole role,
        string content,
        IEnumerable<SourceReference> sourceReferences,
        MessageStatus status)
    {
        Guard.Against.Null(content);
        Guard.Against.Null(sourceReferences);
        Guard.Against.EnumOutOfRange(role);
        Guard.Against.EnumOutOfRange(status);
        Guard.Against.InvalidInput(
            status,
            nameof(status),
            messageStatus => role == MessageRole.Assistant || messageStatus == MessageStatus.Completed,
            $"Only assistant messages can have a non-completed status; received {role} with {status}.");

        if (status == MessageStatus.Completed)
        {
            Guard.Against.NullOrWhiteSpace(content);
        }

        Role = role;
        Content = content;
        Status = status;
        SourceReferences = Array.AsReadOnly(sourceReferences.ToArray());
    }

    public MessageRole Role { get; }

    public string Content { get; }

    public MessageStatus Status { get; }

    public IReadOnlyList<SourceReference> SourceReferences { get; }
}
