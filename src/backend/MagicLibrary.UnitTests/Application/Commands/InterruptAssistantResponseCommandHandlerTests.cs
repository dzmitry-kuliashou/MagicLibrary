using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validators;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Core.ValueObjects;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class InterruptAssistantResponseCommandHandlerTests
{
    [Fact]
    public async Task Handle_InvalidConversationId_ReturnsValidationError()
    {
        var readRepository = new Mock<IConversationReadRepository>();
        var writeRepository = new Mock<IConversationWriteRepository>();
        var handler = new InterruptAssistantResponseCommandHandler(
            new InterruptAssistantResponseCommandValidator(),
            readRepository.Object,
            writeRepository.Object,
            TimeProvider.System);

        var result = await handler.Handle(
            new InterruptAssistantResponseCommand(Guid.Empty),
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
    public async Task Handle_ValidCommand_PersistsInterruptedStatusWithoutContent()
    {
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var pendingAt = createdAt.AddMinutes(1);
        var updatedAt = createdAt.AddMinutes(2);
        var conversation = Conversation.Create("Research", createdAt, "System instructions");
        conversation.BeginAssistantResponse(pendingAt);
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(conversation.Id, CancellationToken.None))
            .ReturnsAsync(conversation);
        var writeRepository = new Mock<IConversationWriteRepository>();
        writeRepository
            .Setup(repository => repository.UpdateAsync(conversation, CancellationToken.None))
            .Returns((Conversation updatedConversation, CancellationToken _) =>
                Task.FromResult(updatedConversation));
        var handler = new InterruptAssistantResponseCommandHandler(
            new InterruptAssistantResponseCommandValidator(),
            readRepository.Object,
            writeRepository.Object,
            new FixedTimeProvider(updatedAt));

        var result = await handler.Handle(
            new InterruptAssistantResponseCommand(conversation.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(updatedAt, result.Value.UpdatedAt);
        var message = result.Value.Messages[^1];
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.Equal(MessageStatus.Interrupted, message.Status);
        Assert.Empty(message.Content);
        Assert.Empty(message.SourceReferences);
        writeRepository.Verify(
            repository => repository.UpdateAsync(conversation, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LastMessageNotAssistantPending_ReturnsConflictWithoutPersisting()
    {
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var conversation = Conversation.Create("Research", createdAt, "System instructions");
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(conversation.Id, CancellationToken.None))
            .ReturnsAsync(conversation);
        var writeRepository = new Mock<IConversationWriteRepository>();
        var handler = new InterruptAssistantResponseCommandHandler(
            new InterruptAssistantResponseCommandValidator(),
            readRepository.Object,
            writeRepository.Object,
            TimeProvider.System);

        var result = await handler.Handle(
            new InterruptAssistantResponseCommand(conversation.Id),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(ConversationErrors.AssistantResponseNotPending.Code, error.Code);
        writeRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Conversation>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
