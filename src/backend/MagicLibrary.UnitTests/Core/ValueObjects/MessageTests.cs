using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.ValueObjects;
using Xunit;

namespace MagicLibrary.UnitTests.Core.ValueObjects;

public sealed class MessageTests
{
    [Fact]
    public void AddUserMessage_CreatesMessageWithUserRoleAndContent()
    {
        var conversation = CreateConversation();

        conversation.AddUserMessage("Find the chapter about search.", CreatedAt.AddMinutes(1));

        var message = conversation.Messages[^1];
        Assert.Equal(MessageRole.User, message.Role);
        Assert.Equal("Find the chapter about search.", message.Content);
        Assert.Empty(message.SourceReferences);
    }

    [Fact]
    public void AddAssistantMessage_CreatesMessageWithReferences()
    {
        var conversation = CreateConversation();
        var references = new List<SourceReference> { CreateSourceReference() };

        conversation.AddAssistantMessage("The answer is in chapter 4.", references, CreatedAt.AddMinutes(1));
        references.Clear();

        var message = conversation.Messages[^1];
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.Equal("The answer is in chapter 4.", message.Content);
        Assert.Collection(
            message.SourceReferences,
            reference => Assert.Equal("Book title", reference.BookTitle));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<SourceReference>)message.SourceReferences).Clear());
    }

    private static DateTimeOffset CreatedAt => new(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);

    private static Conversation CreateConversation()
    {
        return Conversation.Create("Research", CreatedAt, "System instructions");
    }

    private static SourceReference CreateSourceReference()
    {
        return SourceReference.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Book title",
            4);
    }
}
