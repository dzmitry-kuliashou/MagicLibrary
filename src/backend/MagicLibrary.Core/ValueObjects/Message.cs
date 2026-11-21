using Ardalis.GuardClauses;

namespace MagicLibrary.Core.ValueObjects;

public enum MessageRole
{
    System,
    User,
    Assistant
}

public sealed record Message
{
    public static Message Create(
        MessageRole role,
        string content,
        IEnumerable<SourceReference> sourceReferences)
    {
        return new Message(role, content, sourceReferences);
    }

    private Message(MessageRole role, string content, IEnumerable<SourceReference> sourceReferences)
    {
        Guard.Against.NullOrWhiteSpace(content);
        Guard.Against.Null(sourceReferences);

        Role = role;
        Content = content;
        SourceReferences = Array.AsReadOnly(sourceReferences.ToArray());
    }

    public MessageRole Role { get; }

    public string Content { get; }

    public IReadOnlyList<SourceReference> SourceReferences { get; }
}
