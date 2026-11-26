using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.ValueObjects;
using Xunit;

namespace MagicLibrary.UnitTests.Core.Aggregates;

public sealed class ConversationTests
{
    [Fact]
    public void Create_NameAndSystemMessage_AddsSystemMessageFirst()
    {
        var createdAt = new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);

        var conversation = Conversation.Create("Research", createdAt, "Answer with sources.");

        Assert.NotEqual(Guid.Empty, conversation.Id);
        Assert.Equal("Research", conversation.Name);
        Assert.Equal(createdAt, conversation.CreatedAt);
        Assert.Equal(createdAt, conversation.UpdatedAt);
        var systemMessage = Assert.Single(conversation.Messages);
        Assert.Equal(MessageRole.System, systemMessage.Role);
        Assert.Equal("Answer with sources.", systemMessage.Content);
        Assert.Empty(systemMessage.SourceReferences);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankName_RejectsValue(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => Conversation.Create(
            name!,
            new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero),
            "System instructions"));
    }

    [Fact]
    public void Create_DefaultTimestamp_RejectsValue()
    {
        Assert.ThrowsAny<ArgumentException>(() => Conversation.Create(
            "Research",
            default,
            "System instructions"));
    }

    [Fact]
    public void Reconstitute_PersistedState_RestoresConversationAndNestedMessages()
    {
        var id = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.FromHours(-5));
        var updatedAt = createdAt.AddMinutes(2);
        var messages = new[]
        {
            Message.Create(MessageRole.System, "System instructions", []),
            Message.Create(MessageRole.User, "Question", []),
            Message.Create(
                MessageRole.Assistant,
                "Answer",
                [SourceReference.Create(bookId, "Book title", 4, chunkId)])
        };

        var conversation = Conversation.Reconstitute(
            id,
            "Research",
            createdAt,
            updatedAt,
            messages);

        Assert.Equal(id, conversation.Id);
        Assert.Equal("Research", conversation.Name);
        Assert.Equal(createdAt, conversation.CreatedAt);
        Assert.Equal(createdAt.Offset, conversation.CreatedAt.Offset);
        Assert.Equal(updatedAt, conversation.UpdatedAt);
        Assert.Equal(updatedAt.Offset, conversation.UpdatedAt.Offset);
        Assert.Equal(3, conversation.Messages.Count);
        Assert.Equal(MessageRole.System, conversation.Messages[0].Role);
        Assert.Equal("System instructions", conversation.Messages[0].Content);
        Assert.Equal(MessageRole.User, conversation.Messages[1].Role);
        Assert.Equal("Question", conversation.Messages[1].Content);
        Assert.Equal(MessageRole.Assistant, conversation.Messages[2].Role);
        Assert.Equal("Answer", conversation.Messages[2].Content);
        var sourceReference = Assert.Single(conversation.Messages[2].SourceReferences);
        Assert.Equal(bookId, sourceReference.BookId);
        Assert.Equal("Book title", sourceReference.BookTitle);
        Assert.Equal(4, sourceReference.PageNumber);
        Assert.Equal(chunkId, sourceReference.ChunkId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankSystemMessage_RejectsValue(string? systemMessageContent)
    {
        Assert.ThrowsAny<ArgumentException>(() => Conversation.Create(
            "Research",
            new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero),
            systemMessageContent!));
    }

    [Fact]
    public void AddUserMessage_Content_AppendsUserMessage()
    {
        var conversation = CreateConversation();
        var updatedAt = new DateTimeOffset(2026, 4, 5, 6, 9, 8, TimeSpan.Zero);

        conversation.AddUserMessage("Find the chapter about search.", updatedAt);

        var message = conversation.Messages[^1];
        Assert.Equal(MessageRole.User, message.Role);
        Assert.Equal("Find the chapter about search.", message.Content);
        Assert.Empty(message.SourceReferences);
        Assert.Equal(updatedAt, conversation.UpdatedAt);
    }

    [Fact]
    public void BeginAssistantResponse_AddsPendingMessage()
    {
        var conversation = CreateConversation();
        conversation.AddUserMessage(
            "Find the chapter about search.",
            new DateTimeOffset(2026, 4, 5, 6, 9, 8, TimeSpan.Zero));
        var previousMessages = conversation.Messages.ToArray();
        var updatedAt = new DateTimeOffset(2026, 4, 5, 6, 10, 8, TimeSpan.Zero);

        var assistantMessage = conversation.BeginAssistantResponse(updatedAt);

        Assert.Equal(previousMessages.Length + 1, conversation.Messages.Count);
        Assert.Same(previousMessages[0], conversation.Messages[0]);
        Assert.Same(previousMessages[1], conversation.Messages[1]);
        Assert.Same(assistantMessage, conversation.Messages[^1]);
        Assert.Equal(MessageRole.Assistant, assistantMessage.Role);
        Assert.Equal(MessageStatus.Pending, assistantMessage.Status);
        Assert.Empty(assistantMessage.Content);
        Assert.Empty(assistantMessage.SourceReferences);
        Assert.Equal(updatedAt, conversation.UpdatedAt);
    }

    [Fact]
    public void CompleteAssistantResponse_StoresFullAnswerAndMarksCompleted()
    {
        var conversation = CreateConversation();
        conversation.AddUserMessage(
            "Find the chapter about search.",
            new DateTimeOffset(2026, 4, 5, 6, 9, 8, TimeSpan.Zero));
        var pendingMessage = conversation.BeginAssistantResponse(
            new DateTimeOffset(2026, 4, 5, 6, 10, 8, TimeSpan.Zero));
        var previousMessages = conversation.Messages.Take(2).ToArray();
        var answer = "The full answer assembled from every streamed delta.";
        var updatedAt = new DateTimeOffset(2026, 4, 5, 6, 11, 8, TimeSpan.Zero);

        conversation.CompleteAssistantResponse(answer, [], updatedAt);

        Assert.Equal(previousMessages.Length + 1, conversation.Messages.Count);
        Assert.Same(previousMessages[0], conversation.Messages[0]);
        Assert.Same(previousMessages[1], conversation.Messages[1]);
        var completedMessage = conversation.Messages[^1];
        Assert.NotSame(pendingMessage, completedMessage);
        Assert.Equal(MessageRole.Assistant, completedMessage.Role);
        Assert.Equal(answer, completedMessage.Content);
        Assert.Equal(MessageStatus.Completed, completedMessage.Status);
        Assert.Empty(completedMessage.SourceReferences);
        Assert.Equal(updatedAt, conversation.UpdatedAt);
    }

    [Theory]
    [InlineData(MessageStatus.Failed)]
    [InlineData(MessageStatus.Interrupted)]
    public void FailOrInterruptAssistantResponse_FromPending_StoresTerminalStatusWithoutContent(
        MessageStatus terminalStatus)
    {
        var conversation = CreateConversation();
        conversation.AddUserMessage(
            "Find the chapter about search.",
            new DateTimeOffset(2026, 4, 5, 6, 9, 8, TimeSpan.Zero));
        var pendingMessage = conversation.BeginAssistantResponse(
            new DateTimeOffset(2026, 4, 5, 6, 10, 8, TimeSpan.Zero));
        var previousMessages = conversation.Messages.Take(2).ToArray();
        var updatedAt = new DateTimeOffset(2026, 4, 5, 6, 11, 8, TimeSpan.Zero);

        if (terminalStatus == MessageStatus.Failed)
        {
            conversation.FailAssistantResponse(updatedAt);
        }
        else
        {
            conversation.InterruptAssistantResponse(updatedAt);
        }

        Assert.Equal(previousMessages.Length + 1, conversation.Messages.Count);
        Assert.Same(previousMessages[0], conversation.Messages[0]);
        Assert.Same(previousMessages[1], conversation.Messages[1]);
        var terminalMessage = conversation.Messages[^1];
        Assert.NotSame(pendingMessage, terminalMessage);
        Assert.Equal(MessageRole.Assistant, terminalMessage.Role);
        Assert.Equal(terminalStatus, terminalMessage.Status);
        Assert.Empty(terminalMessage.Content);
        Assert.Empty(terminalMessage.SourceReferences);
        Assert.Equal(updatedAt, conversation.UpdatedAt);
    }

    [Fact]
    public void AddUserMessage_WhenLastMessagePending_RejectsWithoutChangingHistory()
    {
        var conversation = CreateConversation();
        conversation.AddUserMessage(
            "Find the chapter about search.",
            new DateTimeOffset(2026, 4, 5, 6, 9, 8, TimeSpan.Zero));
        conversation.BeginAssistantResponse(
            new DateTimeOffset(2026, 4, 5, 6, 10, 8, TimeSpan.Zero));
        var previousMessages = conversation.Messages.ToArray();
        var previousUpdatedAt = conversation.UpdatedAt;

        Assert.ThrowsAny<ArgumentException>(() => conversation.AddUserMessage(
            "Another question",
            previousUpdatedAt.AddMinutes(1)));

        Assert.Equal(previousMessages.Length, conversation.Messages.Count);
        for (var index = 0; index < previousMessages.Length; index++)
        {
            Assert.Same(previousMessages[index], conversation.Messages[index]);
        }

        Assert.Equal(previousUpdatedAt, conversation.UpdatedAt);
    }

    [Fact]
    public void BeginAssistantResponse_WhenLastMessagePending_RejectsWithoutChangingHistory()
    {
        var conversation = CreateConversation();
        conversation.AddUserMessage(
            "Find the chapter about search.",
            new DateTimeOffset(2026, 4, 5, 6, 9, 8, TimeSpan.Zero));
        conversation.BeginAssistantResponse(
            new DateTimeOffset(2026, 4, 5, 6, 10, 8, TimeSpan.Zero));
        var previousMessages = conversation.Messages.ToArray();
        var previousUpdatedAt = conversation.UpdatedAt;

        Assert.ThrowsAny<ArgumentException>(() =>
            conversation.BeginAssistantResponse(previousUpdatedAt.AddMinutes(1)));

        Assert.Equal(previousMessages.Length, conversation.Messages.Count);
        for (var index = 0; index < previousMessages.Length; index++)
        {
            Assert.Same(previousMessages[index], conversation.Messages[index]);
        }

        Assert.Equal(previousUpdatedAt, conversation.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddUserMessage_BlankContent_RejectsValueWithoutChangingHistory(string? content)
    {
        var conversation = CreateConversation();
        var originalUpdatedAt = conversation.UpdatedAt;
        var originalCount = conversation.Messages.Count;

        Assert.ThrowsAny<ArgumentException>(() => conversation.AddUserMessage(
            content!,
            originalUpdatedAt.AddMinutes(1)));

        Assert.Equal(originalUpdatedAt, conversation.UpdatedAt);
        Assert.Equal(originalCount, conversation.Messages.Count);
    }

    [Fact]
    public void AddUserMessage_DefaultTimestamp_RejectsValueWithoutChangingHistory()
    {
        var conversation = CreateConversation();
        var originalUpdatedAt = conversation.UpdatedAt;
        var originalCount = conversation.Messages.Count;

        Assert.ThrowsAny<ArgumentException>(() => conversation.AddUserMessage("Question", default));

        Assert.Equal(originalUpdatedAt, conversation.UpdatedAt);
        Assert.Equal(originalCount, conversation.Messages.Count);
    }

    [Fact]
    public void Messages_ReadOnlyView_CannotMutateConversationHistory()
    {
        var conversation = CreateConversation();

        Assert.Throws<NotSupportedException>(() =>
            ((IList<Message>)conversation.Messages).Clear());

        Assert.Single(conversation.Messages);
    }

    private static Conversation CreateConversation()
    {
        return Conversation.Create(
            "Research",
            new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero),
            "System instructions");
    }

    private static SourceReference CreateSourceReference()
    {
        return SourceReference.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Book title",
            4);
    }
}
