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

public sealed class AddAssistantCommandHandlerTests
{
    [Fact]
    public async Task Handle_BlankContent_ReturnsValidationError()
    {
        var readRepository = new Mock<IConversationReadRepository>();
        var writeRepository = new Mock<IConversationWriteRepository>();
        var handler = new AddAssistantCommandHandler(
            new AddAssistantCommandValidator(),
            readRepository.Object,
            writeRepository.Object,
            TimeProvider.System);

        var result = await handler.Handle(
            new AddAssistantCommand(Guid.NewGuid(), " ", []),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(ConversationErrors.MissingContent.Code, error.Code);
        Assert.Equal(nameof(AddAssistantCommand.Content), error.Metadata?["PropertyName"]);
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
        var handler = new AddAssistantCommandHandler(
            new AddAssistantCommandValidator(),
            readRepository.Object,
            writeRepository.Object,
            TimeProvider.System);

        var result = await handler.Handle(
            new AddAssistantCommand(conversationId, "Answer", []),
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

    [Fact]
    public async Task Handle_NoReferences_AppendsAndPersistsAssistantMessage()
    {
        const string content = "I can help with that book.";
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(1);
        var conversation = Conversation.Create("Research", createdAt, "System instructions");
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(conversation.Id, CancellationToken.None))
            .ReturnsAsync(conversation);
        var writeRepository = new Mock<IConversationWriteRepository>();
        writeRepository
            .Setup(repository => repository.UpdateAsync(conversation, CancellationToken.None))
            .Returns((Conversation updatedConversation, CancellationToken _) =>
                Task.FromResult(updatedConversation));
        var handler = new AddAssistantCommandHandler(
            new AddAssistantCommandValidator(),
            readRepository.Object,
            writeRepository.Object,
            new FixedTimeProvider(updatedAt));

        var result = await handler.Handle(
            new AddAssistantCommand(conversation.Id, content, []),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(updatedAt, result.Value.UpdatedAt);
        var message = result.Value.Messages[^1];
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.Equal(content, message.Content);
        Assert.Empty(message.SourceReferences);
        writeRepository.Verify(
            repository => repository.UpdateAsync(conversation, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReferencesProvided_AppendsAndPersistsAssistantMessage()
    {
        const string content = "The answer is in chapter 4.";
        const string bookTitle = "Library Guide";
        const int pageNumber = 4;
        var bookId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var reference = SourceReference.Create(bookId, bookTitle, pageNumber, chunkId);
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(1);
        var conversation = Conversation.Create("Research", createdAt, "System instructions");
        var readRepository = new Mock<IConversationReadRepository>();
        readRepository
            .Setup(repository => repository.GetByIdAsync(conversation.Id, CancellationToken.None))
            .ReturnsAsync(conversation);
        var writeRepository = new Mock<IConversationWriteRepository>();
        writeRepository
            .Setup(repository => repository.UpdateAsync(conversation, CancellationToken.None))
            .Returns((Conversation updatedConversation, CancellationToken _) =>
                Task.FromResult(updatedConversation));
        var handler = new AddAssistantCommandHandler(
            new AddAssistantCommandValidator(),
            readRepository.Object,
            writeRepository.Object,
            new FixedTimeProvider(updatedAt));

        var result = await handler.Handle(
            new AddAssistantCommand(conversation.Id, content, [reference]),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(updatedAt, result.Value.UpdatedAt);
        var message = result.Value.Messages[^1];
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.Equal(content, message.Content);
        var sourceReference = Assert.Single(message.SourceReferences);
        Assert.Equal(bookId, sourceReference.BookId);
        Assert.Equal(bookTitle, sourceReference.BookTitle);
        Assert.Equal(pageNumber, sourceReference.PageNumber);
        Assert.Equal(chunkId, sourceReference.ChunkId);
        writeRepository.Verify(
            repository => repository.UpdateAsync(conversation, CancellationToken.None),
            Times.Once);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
