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
    public void AddAssistantMessage_ContentAndReferences_AppendsAssistantMessage()
    {
        var conversation = CreateConversation();
        var references = new[] { CreateSourceReference() };
        var updatedAt = new DateTimeOffset(2026, 4, 5, 6, 10, 8, TimeSpan.Zero);

        conversation.AddAssistantMessage("The answer is in chapter 4.", references, updatedAt);

        var message = conversation.Messages[^1];
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.Equal("The answer is in chapter 4.", message.Content);
        Assert.Equal(references, message.SourceReferences);
        Assert.Equal(updatedAt, conversation.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddAssistantMessage_BlankContent_RejectsValueWithoutChangingHistory(string? content)
    {
        var conversation = CreateConversation();
        var originalUpdatedAt = conversation.UpdatedAt;
        var originalCount = conversation.Messages.Count;

        Assert.ThrowsAny<ArgumentException>(() => conversation.AddAssistantMessage(
            content!,
            [],
            originalUpdatedAt.AddMinutes(1)));

        Assert.Equal(originalUpdatedAt, conversation.UpdatedAt);
        Assert.Equal(originalCount, conversation.Messages.Count);
    }

    [Fact]
    public void AddAssistantMessage_NullSourceReferences_RejectsValueWithoutChangingHistory()
    {
        var conversation = CreateConversation();
        var originalUpdatedAt = conversation.UpdatedAt;
        var originalCount = conversation.Messages.Count;

        Assert.Throws<ArgumentNullException>(() => conversation.AddAssistantMessage(
            "Answer",
            null!,
            originalUpdatedAt.AddMinutes(1)));

        Assert.Equal(originalUpdatedAt, conversation.UpdatedAt);
        Assert.Equal(originalCount, conversation.Messages.Count);
    }

    [Fact]
    public void AddAssistantMessage_DefaultTimestamp_RejectsValueWithoutChangingHistory()
    {
        var conversation = CreateConversation();
        var originalUpdatedAt = conversation.UpdatedAt;
        var originalCount = conversation.Messages.Count;

        Assert.ThrowsAny<ArgumentException>(() => conversation.AddAssistantMessage(
            "Answer",
            [],
            default));

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
