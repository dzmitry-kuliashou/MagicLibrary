using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Queries;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Queries;

public sealed class GetConversationByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_MissingConversation_ReturnsNotFound()
    {
        var conversationId = Guid.NewGuid();
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(conversationId, CancellationToken.None))
            .ReturnsAsync((Conversation?)null);
        var handler = new GetConversationByIdQueryHandler(readRepository.Object);

        var result = await handler.Handle(
            new GetConversationByIdQuery(conversationId),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        var expectedError = ConversationErrors.ConversationNotFound(conversationId);
        Assert.Equal(expectedError.Type, error.Type);
        Assert.Equal(expectedError.Code, error.Code);
        readRepository.Verify(
            repository => repository.GetByIdAsync(conversationId, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingConversation_ReturnsConversation()
    {
        var conversation = Conversation.Create(
            "Research",
            new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero),
            "System instructions");
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(conversation.Id, CancellationToken.None))
            .ReturnsAsync(conversation);
        var handler = new GetConversationByIdQueryHandler(readRepository.Object);

        var result = await handler.Handle(
            new GetConversationByIdQuery(conversation.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Same(conversation, result.Value);
        readRepository.Verify(
            repository => repository.GetByIdAsync(conversation.Id, CancellationToken.None),
            Times.Once);
    }
}
