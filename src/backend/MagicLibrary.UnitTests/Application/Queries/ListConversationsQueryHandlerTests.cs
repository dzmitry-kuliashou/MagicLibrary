using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.Requests;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Queries;

public sealed class ListConversationsQueryHandlerTests
{
    [Fact]
    public async Task Handle_OutOfRangePage_ReturnsEmptyItemsAndMetadata()
    {
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.ListAsync(5, 2, CancellationToken.None))
            .ReturnsAsync(((IReadOnlyList<Conversation>)Array.Empty<Conversation>(), 5));
        var handler = new ListConversationsQueryHandler(readRepository.Object);

        var result = await handler.Handle(
            new ListConversationsQuery(new ListParams { Page = "5", PageSize = "2" }),
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.TotalPageCount);
        Assert.Equal(5, result.CurrentPage);
        Assert.Equal(2, result.PageSize);
        Assert.True(result.HasPrevious);
        Assert.False(result.HasNext);
        readRepository.Verify(
            repository => repository.ListAsync(5, 2, CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [InlineData("invalid", "2", 0, 2)]
    [InlineData("-1", "2", 0, 2)]
    [InlineData("2", "invalid", 2, 10)]
    [InlineData("2", "0", 2, 10)]
    [InlineData("2", "-1", 2, 10)]
    public async Task Handle_InvalidParameters_UsesDefaults(
        string page,
        string pageSize,
        int expectedPage,
        int expectedPageSize)
    {
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.ListAsync(
                expectedPage,
                expectedPageSize,
                CancellationToken.None))
            .ReturnsAsync(((IReadOnlyList<Conversation>)Array.Empty<Conversation>(), 0));
        var handler = new ListConversationsQueryHandler(readRepository.Object);

        var result = await handler.Handle(
            new ListConversationsQuery(new ListParams { Page = page, PageSize = pageSize }),
            CancellationToken.None);

        Assert.Equal(expectedPage, result.CurrentPage);
        Assert.Equal(expectedPageSize, result.PageSize);
        readRepository.Verify(
            repository => repository.ListAsync(
                expectedPage,
                expectedPageSize,
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidPage_ReturnsItemsAndPaginationMetadata()
    {
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var conversations = new[]
        {
            Conversation.Create("Research", createdAt, "System instructions"),
            Conversation.Create("History", createdAt.AddMinutes(1), "System instructions")
        };
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.ListAsync(1, 2, CancellationToken.None))
            .ReturnsAsync(((IReadOnlyList<Conversation>)conversations, 5));
        var handler = new ListConversationsQueryHandler(readRepository.Object);

        var result = await handler.Handle(
            new ListConversationsQuery(new ListParams { Page = "1", PageSize = "2" }),
            CancellationToken.None);

        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal(conversations[0].Id, item.Id);
                Assert.Equal(conversations[0].Name, item.Name);
                Assert.Equal(conversations[0].CreatedAt, item.CreatedAt);
                Assert.Equal(conversations[0].UpdatedAt, item.UpdatedAt);
            },
            item =>
            {
                Assert.Equal(conversations[1].Id, item.Id);
                Assert.Equal(conversations[1].Name, item.Name);
                Assert.Equal(conversations[1].CreatedAt, item.CreatedAt);
                Assert.Equal(conversations[1].UpdatedAt, item.UpdatedAt);
            });
        Assert.Equal(3, result.TotalPageCount);
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(2, result.PageSize);
        Assert.True(result.HasPrevious);
        Assert.True(result.HasNext);
        readRepository.Verify(
            repository => repository.ListAsync(1, 2, CancellationToken.None),
            Times.Once);
    }
}
