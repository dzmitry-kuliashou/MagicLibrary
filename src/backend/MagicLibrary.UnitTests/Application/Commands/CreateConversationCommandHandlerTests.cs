using ErrorOr;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validators;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Core.ValueObjects;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class CreateConversationCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesConversationWithSystemMessage()
    {
        const string name = "Research";
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var writeRepository = new Mock<IConversationWriteRepository>();
        writeRepository
            .Setup(repository => repository.CreateAsync(
                It.IsAny<Conversation>(),
                CancellationToken.None))
            .Returns((Conversation conversation, CancellationToken _) => Task.FromResult(conversation));
        var handler = new CreateConversationCommandHandler(
            new CreateConversationCommandValidator(),
            new FixedTimeProvider(createdAt),
            writeRepository.Object);

        var result = await handler.Handle(
            new CreateConversationCommand(name),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(name, result.Value.Name);
        Assert.Equal(createdAt, result.Value.CreatedAt);
        var message = Assert.Single(result.Value.Messages);
        Assert.Equal(MessageRole.System, message.Role);
        Assert.Equal(LibraryChatInstructions.SystemMessage, message.Content);
        Assert.Contains("retrieved document chunk content", message.Content);
        Assert.Contains("not factual evidence", message.Content);
        Assert.Contains("does not provide enough information", message.Content);
        Assert.Contains("politely decline", message.Content);
        Assert.DoesNotContain("search_books", message.Content);
        Assert.DoesNotContain("search_chunks", message.Content);
        writeRepository.Verify(
            repository => repository.CreateAsync(
                It.IsAny<Conversation>(),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_BlankName_ReturnsValidationError()
    {
        var writeRepository = new Mock<IConversationWriteRepository>();
        var handler = new CreateConversationCommandHandler(
            new CreateConversationCommandValidator(),
            TimeProvider.System,
            writeRepository.Object);

        var result = await handler.Handle(
            new CreateConversationCommand(" "),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(ConversationErrors.MissingName.Code, error.Code);
        Assert.Equal(nameof(CreateConversationCommand.Name), error.Metadata?["PropertyName"]);
        writeRepository.Verify(
            repository => repository.CreateAsync(
                It.IsAny<Conversation>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
