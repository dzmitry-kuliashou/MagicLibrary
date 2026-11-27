using MagicLibrary.Application.Queries;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Queries;

public sealed class ListBookConversationsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSummariesForRequestedBook()
    {
        var bookId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var conversations = new[]
        {
            Conversation.Create("First", createdAt, "System instructions", bookId),
            Conversation.Create("Second", createdAt.AddMinutes(1), "System instructions", bookId)
        };
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.ListBookConversationsAsync(bookId, CancellationToken.None))
            .ReturnsAsync((IReadOnlyList<Conversation>)conversations);
        var handler = new ListBookConversationsQueryHandler(readRepository.Object);

        var result = await handler.Handle(
            new ListBookConversationsQuery(bookId),
            CancellationToken.None);

        Assert.Equal(conversations.Length, result.Count);
        for (var index = 0; index < conversations.Length; index++)
        {
            Assert.Equal(conversations[index].Id, result[index].Id);
            Assert.Equal(conversations[index].Name, result[index].Name);
            Assert.Equal(conversations[index].CreatedAt, result[index].CreatedAt);
            Assert.Equal(conversations[index].UpdatedAt, result[index].UpdatedAt);
        }

        readRepository.Verify(
            repository => repository.ListBookConversationsAsync(bookId, CancellationToken.None),
            Times.Once);
    }
}
