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

    private Conversation(
        Guid id,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IEnumerable<Message> messages)
        : base(id)
    {
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        _messages.AddRange(messages);
    }

    public string Name { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<Message> Messages => Array.AsReadOnly(_messages.ToArray());

    public static Conversation Create(string name, DateTimeOffset createdAt, string systemMessageContent)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Default(createdAt);
        Guard.Against.NullOrWhiteSpace(systemMessageContent);

        var conversation = new Conversation(name, createdAt);
        conversation._messages.Add(Message.Create(MessageRole.System, systemMessageContent, []));
        return conversation;
    }

    internal static Conversation Reconstitute(
        Guid id,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IEnumerable<Message> messages)
    {
        // Persisted state is trusted; write-side APIs validate new aggregates.
        return new Conversation(id, name, createdAt, updatedAt, messages);
    }

    public void AddUserMessage(string content, DateTimeOffset updatedAt)
    {
        Guard.Against.NullOrWhiteSpace(content);
        Guard.Against.Default(updatedAt);
        EnsureNoPendingAssistantResponse();

        _messages.Add(Message.Create(MessageRole.User, content, []));
        UpdatedAt = updatedAt;
    }

    public Message BeginAssistantResponse(DateTimeOffset updatedAt)
    {
        Guard.Against.Default(updatedAt);
        EnsureNoPendingAssistantResponse();

        var message = Message.Create(
            MessageRole.Assistant,
            string.Empty,
            [],
            MessageStatus.Pending);

        _messages.Add(message);
        UpdatedAt = updatedAt;
        return message;
    }

    public void CompleteAssistantResponse(
        string content,
        IEnumerable<SourceReference> sourceReferences,
        DateTimeOffset updatedAt)
    {
        Guard.Against.NullOrWhiteSpace(content);
        Guard.Against.Null(sourceReferences);
        Guard.Against.Default(updatedAt);

        ReplaceAssistantResponse(content, sourceReferences, MessageStatus.Completed, updatedAt);
    }

    public void FailAssistantResponse(DateTimeOffset updatedAt)
    {
        Guard.Against.Default(updatedAt);

        ReplaceAssistantResponse(string.Empty, [], MessageStatus.Failed, updatedAt);
    }

    public void InterruptAssistantResponse(DateTimeOffset updatedAt)
    {
        Guard.Against.Default(updatedAt);

        ReplaceAssistantResponse(string.Empty, [], MessageStatus.Interrupted, updatedAt);
    }

    private void EnsureNoPendingAssistantResponse()
    {
        Guard.Against.InvalidInput(
            _messages,
            nameof(Messages),
            messages => messages.Count == 0
                || messages[^1].Status != MessageStatus.Pending,
            "Cannot add a message while the latest conversation message is a Pending assistant response.");
    }

    private void ReplaceAssistantResponse(
        string content,
        IEnumerable<SourceReference> sourceReferences,
        MessageStatus status,
        DateTimeOffset updatedAt)
    {
        Guard.Against.InvalidInput(
            _messages,
            nameof(Messages),
            messages => messages.Count > 0 && messages[^1].Status == MessageStatus.Pending,
            $"Cannot replace assistant response with status {status} unless the latest message is a pending assistant message.");

        var pendingMessage = _messages[^1];
        _messages[^1] = Message.Create(
            MessageRole.Assistant,
            content,
            sourceReferences,
            status);
        UpdatedAt = updatedAt;
    }
}
