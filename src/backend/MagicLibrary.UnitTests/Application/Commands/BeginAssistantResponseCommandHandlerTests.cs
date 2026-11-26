using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validators;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class BeginAssistantResponseCommandHandlerTests
{
    [Fact]
    public async Task Handle_InvalidConversationId_ReturnsValidationError()
    {
        var readRepository = new Mock<IConversationReadRepository>();
        var writeRepository = new Mock<IConversationWriteRepository>();
        var handler = new BeginAssistantResponseCommandHandler(
            new BeginAssistantResponseCommandValidator(),
            readRepository.Object,
            writeRepository.Object,
            TimeProvider.System);

        var result = await handler.Handle(
            new BeginAssistantResponseCommand(Guid.Empty),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(ConversationErrors.InvalidId.Code, error.Code);
        readRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Conversation>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_MissingConversation_ReturnsNotFound()
    {
        var conversationId = Guid.NewGuid();
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(conversationId, CancellationToken.None))
            .ReturnsAsync((Conversation?)null);
        var writeRepository = new Mock<IConversationWriteRepository>();
        var handler = new BeginAssistantResponseCommandHandler(
            new BeginAssistantResponseCommandValidator(),
            readRepository.Object,
            writeRepository.Object,
            TimeProvider.System);

        var result = await handler.Handle(
            new BeginAssistantResponseCommand(conversationId),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(ConversationErrors.ConversationNotFound(conversationId).Code, error.Code);
        readRepository.Verify(
            repository => repository.GetByIdAsync(conversationId, CancellationToken.None),
            Times.Once);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Conversation>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
