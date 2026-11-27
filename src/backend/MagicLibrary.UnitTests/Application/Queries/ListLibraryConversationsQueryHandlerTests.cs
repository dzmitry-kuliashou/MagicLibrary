using MagicLibrary.Application.Queries;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Queries;

public sealed class ListLibraryConversationsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSummariesFromLibraryConversationRepository()
    {
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var conversations = Enumerable.Range(0, 10)
            .Select(index => Conversation.Create(
                $"Library conversation {index}",
                createdAt.AddMinutes(10 - index),
                "System instructions"))
            .ToArray();
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.ListLibraryConversationsAsync(CancellationToken.None))
            .ReturnsAsync((IReadOnlyList<Conversation>)conversations);
        var handler = new ListLibraryConversationsQueryHandler(readRepository.Object);

        var result = await handler.Handle(new ListLibraryConversationsQuery(), CancellationToken.None);

        Assert.Equal(conversations.Length, result.Count);
        for (var index = 0; index < conversations.Length; index++)
        {
            Assert.Equal(conversations[index].Id, result[index].Id);
            Assert.Equal(conversations[index].Name, result[index].Name);
            Assert.Equal(conversations[index].CreatedAt, result[index].CreatedAt);
            Assert.Equal(conversations[index].UpdatedAt, result[index].UpdatedAt);
        }

        readRepository.Verify(
            repository => repository.ListLibraryConversationsAsync(CancellationToken.None),
            Times.Once);
    }
}
