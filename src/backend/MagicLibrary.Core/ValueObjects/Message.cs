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
    internal Message(MessageRole role, string content, IEnumerable<SourceReference> sourceReferences)
    {
        Guard.Against.NullOrWhiteSpace(content, nameof(content));
        Guard.Against.Null(sourceReferences, nameof(sourceReferences));

        Role = role;
        Content = content;
        SourceReferences = Array.AsReadOnly(sourceReferences.ToArray());
    }

    public MessageRole Role { get; }

    public string Content { get; }

    public IReadOnlyList<SourceReference> SourceReferences { get; }
}
