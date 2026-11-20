using Ardalis.GuardClauses;
using MagicLibrary.Core.ValueObjects;

namespace MagicLibrary.Core.Aggregates;

public sealed class Conversation : AggregateRoot
{
    private readonly List<Message> _messages = [];

    private Conversation(string name, DateTimeOffset createdAt)
    {
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public string Name { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<Message> Messages => Array.AsReadOnly(_messages.ToArray());

    public static Conversation Create(string name, DateTimeOffset createdAt, string systemMessageContent)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.Default(createdAt, nameof(createdAt));
        Guard.Against.NullOrWhiteSpace(systemMessageContent, nameof(systemMessageContent));

        var conversation = new Conversation(name, createdAt);
        conversation._messages.Add(new Message(MessageRole.System, systemMessageContent, []));
        return conversation;
    }

    public void AddUserMessage(string content, DateTimeOffset updatedAt)
    {
        Guard.Against.NullOrWhiteSpace(content, nameof(content));
        Guard.Against.Default(updatedAt, nameof(updatedAt));

        _messages.Add(new Message(MessageRole.User, content, []));
        UpdatedAt = updatedAt;
    }

    public void AddAssistantMessage(
        string content,
        IEnumerable<SourceReference> sourceReferences,
        DateTimeOffset updatedAt)
    {
        Guard.Against.NullOrWhiteSpace(content, nameof(content));
        Guard.Against.Null(sourceReferences, nameof(sourceReferences));
        Guard.Against.Default(updatedAt, nameof(updatedAt));

        _messages.Add(new Message(MessageRole.Assistant, content, sourceReferences));
        UpdatedAt = updatedAt;
    }
}
