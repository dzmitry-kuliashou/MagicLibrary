using System.Runtime.CompilerServices;
using ErrorOr;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Conversations;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Queries;
using MagicLibrary.Application.Validators;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.ValueObjects;
using MediatR;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Conversations;

public sealed class LibraryConversationOrchestratorTests
{
    private static TestFixture CreateFixture() => new();

    [Fact]
    public async Task StreamAsync_InvalidMessage_ReturnsValidationErrorWithoutSideEffects()
    {
        var fixture = CreateFixture();

        var result = await fixture.Orchestrator.StreamAsync(
            new LibraryChatCommand(null, " "),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(ConversationErrors.MissingContent.Code, error.Code);
        fixture.Sender.VerifyNoOtherCalls();
        Assert.Null(fixture.ChatAgent.Messages);
    }

    [Fact]
    public async Task StreamAsync_NewConversation_CreatesNamedConversationAndPersistsUserTurn()
    {
        const string question =
            "Find the chapter about search, and explain how the library catalog helps readers locate books.";
        const string systemMessage = "System instructions";
        const string answer = "The search chapter is chapter 4.";
        var conversationName = $"{question[..47]}...";
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(1);
        var completedAt = createdAt.AddMinutes(2);
        var conversation = Conversation.Create(conversationName, createdAt, systemMessage);
        var fixture = CreateFixture();
        fixture.ChatAgent.StreamFactory = (_, _) => GetDeltas(answer);
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<CreateConversationCommand>(),
                CancellationToken.None))
            .ReturnsAsync((ErrorOr<Conversation>)conversation);
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<AddUserMessageCommand>(),
                CancellationToken.None))
            .Returns((AddUserMessageCommand command, CancellationToken _) =>
            {
                conversation.AddUserMessage(command.Content, updatedAt);
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<BeginAssistantResponseCommand>(),
                CancellationToken.None))
            .Returns((BeginAssistantResponseCommand _, CancellationToken _) =>
            {
                conversation.BeginAssistantResponse(updatedAt.AddMinutes(1));
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<CompleteAssistantResponseCommand>(),
                CancellationToken.None))
            .Returns((CompleteAssistantResponseCommand command, CancellationToken _) =>
            {
                conversation.CompleteAssistantResponse(
                    command.Content,
                    command.SourceReferences,
                    completedAt);
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });

        var result = await fixture.Orchestrator.StreamAsync(
            new LibraryChatCommand(null, question),
            CancellationToken.None);
        Assert.False(result.IsError);
        var events = new List<ChatStreamEvent>();
        await foreach (var streamEvent in result.Value)
        {
            events.Add(streamEvent);
        }

        Assert.Equal(conversationName, conversation.Name);
        Assert.Equal(50, new System.Globalization.StringInfo(conversation.Name).LengthInTextElements);
        Assert.Equal(question, conversation.Messages[1].Content);
        Assert.Equal(MessageStatus.Completed, conversation.Messages[^1].Status);
        Assert.Collection(
            fixture.ChatAgent.Messages!,
            message =>
            {
                Assert.Equal(MessageRole.System, message.Role);
                Assert.Equal(systemMessage, message.Content);
            },
            message =>
            {
                Assert.Equal(MessageRole.User, message.Role);
                Assert.Equal(question, message.Content);
            });
        Assert.Equal(new ChatStreamEvent.Status(conversation.Id), events[0]);
        Assert.Contains(new ChatStreamEvent.Text(answer), events);
        Assert.IsType<ChatStreamEvent.Completed>(events[^1]);
        fixture.Sender.Verify(
            service => service.Send(
                It.Is<CreateConversationCommand>(command => command.Name == conversationName),
                CancellationToken.None),
            Times.Once);
        fixture.Sender.Verify(
            service => service.Send(
                It.Is<AddUserMessageCommand>(command =>
                    command.ConversationId == conversation.Id && command.Content == question),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task StreamAsync_ExistingConversation_SendsHistoryAndPersistsCurrentUserTurn()
    {
        const string systemMessage = "System instructions";
        const string previousQuestion = "Where is the search chapter?";
        const string previousAnswer = "It is in chapter 4.";
        const string currentQuestion = "What does the chapter explain?";
        const string answer = "It explains search techniques.";
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(1);
        var conversation = Conversation.Create("Research", createdAt, systemMessage);
        conversation.AddUserMessage(previousQuestion, updatedAt);
        conversation.BeginAssistantResponse(updatedAt.AddMinutes(1));
        conversation.CompleteAssistantResponse(
            previousAnswer,
            [],
            updatedAt.AddMinutes(2));
        var fixture = CreateFixture();
        fixture.ChatAgent.StreamFactory = (_, _) => GetDeltas(answer);
        fixture.Sender
            .Setup(service => service.Send(
            It.Is<GetConversationByIdQuery>(query => query.Id == conversation.Id),
            CancellationToken.None))
            .ReturnsAsync((ErrorOr<Conversation>)conversation);
        fixture.Sender
            .Setup(service => service.Send(
            It.IsAny<AddUserMessageCommand>(),
            CancellationToken.None))
            .Returns((AddUserMessageCommand command, CancellationToken _) =>
            {
            conversation.AddUserMessage(command.Content, updatedAt.AddMinutes(3));
            return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });
        fixture.Sender
            .Setup(service => service.Send(
            It.IsAny<BeginAssistantResponseCommand>(),
            CancellationToken.None))
            .Returns((BeginAssistantResponseCommand _, CancellationToken _) =>
            {
            conversation.BeginAssistantResponse(updatedAt.AddMinutes(4));
            return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });
        fixture.Sender
            .Setup(service => service.Send(
            It.IsAny<CompleteAssistantResponseCommand>(),
            CancellationToken.None))
            .Returns((CompleteAssistantResponseCommand command, CancellationToken _) =>
            {
            conversation.CompleteAssistantResponse(
                command.Content,
                command.SourceReferences,
                updatedAt.AddMinutes(5));
            return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });

        var result = await fixture.Orchestrator.StreamAsync(
            new LibraryChatCommand(conversation.Id, currentQuestion),
            CancellationToken.None);
        Assert.False(result.IsError);
        await foreach (var _ in result.Value)
        {
        }

        Assert.Equal(currentQuestion, conversation.Messages[^2].Content);
        Assert.Equal(MessageStatus.Completed, conversation.Messages[^1].Status);
        Assert.Collection(
            fixture.ChatAgent.Messages!,
            message =>
            {
            Assert.Equal(MessageRole.System, message.Role);
            Assert.Equal(systemMessage, message.Content);
            },
            message =>
            {
            Assert.Equal(MessageRole.User, message.Role);
            Assert.Equal(previousQuestion, message.Content);
            },
            message =>
            {
            Assert.Equal(MessageRole.Assistant, message.Role);
            Assert.Equal(previousAnswer, message.Content);
            },
            message =>
            {
            Assert.Equal(MessageRole.User, message.Role);
            Assert.Equal(currentQuestion, message.Content);
            });
        fixture.Sender.Verify(
            service => service.Send(
            It.Is<GetConversationByIdQuery>(query => query.Id == conversation.Id),
            CancellationToken.None),
            Times.Once);
        fixture.Sender.Verify(
            service => service.Send(
            It.IsAny<CreateConversationCommand>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Sender.Verify(
            service => service.Send(
            It.Is<AddUserMessageCommand>(command =>
                command.ConversationId == conversation.Id && command.Content == currentQuestion),
            CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task StreamAsync_UnknownConversation_ReturnsNotFoundBeforeStreaming()
    {
        var conversationId = Guid.NewGuid();
        var fixture = CreateFixture();
        fixture.Sender
            .Setup(service => service.Send(
            It.Is<GetConversationByIdQuery>(query => query.Id == conversationId),
            CancellationToken.None))
            .ReturnsAsync((ErrorOr<Conversation>)ConversationErrors.ConversationNotFound(conversationId));

        var result = await fixture.Orchestrator.StreamAsync(
            new LibraryChatCommand(conversationId, "Question"),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(ConversationErrors.ConversationNotFound(conversationId).Code, error.Code);
        fixture.Sender.Verify(
            service => service.Send(
            It.Is<GetConversationByIdQuery>(query => query.Id == conversationId),
            CancellationToken.None),
            Times.Once);
        fixture.Sender.Verify(
            service => service.Send(
            It.IsAny<AddUserMessageCommand>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Null(fixture.ChatAgent.Messages);
    }

    [Fact]
    public async Task StreamAsync_CompletedGeneration_PersistsAgentSourceReferencesWithAnswer()
    {
        const string question = "Question";
        const string firstDelta = "The assembled";
        const string secondDelta = " answer.";
        const string expectedAnswer = firstDelta + secondDelta;
        var requestedBookId = Guid.NewGuid();
        var sourceReference = SourceReference.Create(
            requestedBookId,
            "The Library Book",
            12,
            Guid.NewGuid());
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var conversation = Conversation.Create(question, createdAt, "System instructions");
        var fixture = CreateFixture();
        fixture.ChatAgent.ToolStreamFactory = (_, _, _) => GetAgentUpdates(
            new LibraryChatAgentUpdate(firstDelta),
            new LibraryChatAgentUpdate(secondDelta),
            new LibraryChatAgentUpdate(null, [sourceReference]));
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<CreateConversationCommand>(),
                CancellationToken.None))
            .ReturnsAsync((ErrorOr<Conversation>)conversation);
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<AddUserMessageCommand>(),
                CancellationToken.None))
            .Returns((AddUserMessageCommand command, CancellationToken _) =>
            {
                conversation.AddUserMessage(command.Content, createdAt.AddMinutes(1));
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<BeginAssistantResponseCommand>(),
                CancellationToken.None))
            .Returns((BeginAssistantResponseCommand _, CancellationToken _) =>
            {
                conversation.BeginAssistantResponse(createdAt.AddMinutes(2));
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });
        string? persistedAnswer = null;
        IReadOnlyList<SourceReference>? persistedSourceReferences = null;
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<CompleteAssistantResponseCommand>(),
                CancellationToken.None))
            .Returns((CompleteAssistantResponseCommand command, CancellationToken _) =>
            {
                persistedAnswer = command.Content;
                persistedSourceReferences = command.SourceReferences.ToArray();
                conversation.CompleteAssistantResponse(
                    command.Content,
                    command.SourceReferences,
                    createdAt.AddMinutes(3));
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });

        var result = await fixture.Orchestrator.StreamAsync(
            new LibraryChatCommand(null, question, requestedBookId),
            CancellationToken.None);
        Assert.False(result.IsError);
        var events = new List<ChatStreamEvent>();
        await foreach (var streamEvent in result.Value)
        {
            if (streamEvent is ChatStreamEvent.Completed)
            {
                Assert.Equal(expectedAnswer, persistedAnswer);
                Assert.Equal(expectedAnswer, conversation.Messages[^1].Content);
                Assert.Equal(MessageStatus.Completed, conversation.Messages[^1].Status);
                Assert.Equal([sourceReference], persistedSourceReferences);
                Assert.Equal([sourceReference], conversation.Messages[^1].SourceReferences);
            }

            events.Add(streamEvent);
        }

        Assert.Collection(
            events,
            streamEvent => Assert.IsType<ChatStreamEvent.Status>(streamEvent),
            streamEvent => Assert.Equal(new ChatStreamEvent.Text(firstDelta), streamEvent),
            streamEvent => Assert.Equal(new ChatStreamEvent.Text(secondDelta), streamEvent),
            streamEvent => Assert.IsType<ChatStreamEvent.Completed>(streamEvent));
        Assert.Equal(requestedBookId, fixture.ChatAgent.RequestedBookId);
    }

    [Fact]
    public async Task StreamAsync_AgentFailureMarksAssistantFailedAndEmitsError()
    {
        const string question = "Question";
        const string partialAnswer = "Partial answer";
        const string failureMessage = "Generation failed.";
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var conversation = Conversation.Create(question, createdAt, "System instructions");
        var fixture = CreateFixture();
        fixture.ChatAgent.StreamFactory = (_, _) => GetFailedDeltas(partialAnswer, failureMessage);
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<CreateConversationCommand>(),
                CancellationToken.None))
            .ReturnsAsync((ErrorOr<Conversation>)conversation);
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<AddUserMessageCommand>(),
                CancellationToken.None))
            .Returns((AddUserMessageCommand command, CancellationToken _) =>
            {
                conversation.AddUserMessage(command.Content, createdAt.AddMinutes(1));
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<BeginAssistantResponseCommand>(),
                CancellationToken.None))
            .Returns((BeginAssistantResponseCommand _, CancellationToken _) =>
            {
                conversation.BeginAssistantResponse(createdAt.AddMinutes(2));
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<FailAssistantResponseCommand>(),
                CancellationToken.None))
            .Returns((FailAssistantResponseCommand _, CancellationToken _) =>
            {
                conversation.FailAssistantResponse(createdAt.AddMinutes(3));
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });

        var result = await fixture.Orchestrator.StreamAsync(
            new LibraryChatCommand(null, question),
            CancellationToken.None);
        Assert.False(result.IsError);
        var events = new List<ChatStreamEvent>();
        await foreach (var streamEvent in result.Value)
        {
            events.Add(streamEvent);
        }

        Assert.Equal(question, conversation.Messages[1].Content);
        Assert.Equal(MessageStatus.Failed, conversation.Messages[^1].Status);
        Assert.Equal(string.Empty, conversation.Messages[^1].Content);
        Assert.DoesNotContain(events, streamEvent => streamEvent is ChatStreamEvent.Completed);
        Assert.Collection(
            events,
            streamEvent => Assert.IsType<ChatStreamEvent.Status>(streamEvent),
            streamEvent => Assert.Equal(new ChatStreamEvent.Text(partialAnswer), streamEvent),
            streamEvent => Assert.Equal(new ChatStreamEvent.Error(failureMessage), streamEvent));
        fixture.Sender.Verify(
            service => service.Send(
                It.IsAny<FailAssistantResponseCommand>(),
                CancellationToken.None),
            Times.Once);
        fixture.Sender.Verify(
            service => service.Send(
                It.IsAny<CompleteAssistantResponseCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StreamAsync_CancellationMarksAssistantInterrupted()
    {
        using var cancellationSource = new CancellationTokenSource();
        const string question = "Question";
        const string partialAnswer = "Partial answer";
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var conversation = Conversation.Create(question, createdAt, "System instructions");
        var fixture = CreateFixture();
        fixture.ChatAgent.StreamFactory = (_, token) => GetCancellableDeltas(partialAnswer, token);
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<CreateConversationCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ErrorOr<Conversation>)conversation);
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<AddUserMessageCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((AddUserMessageCommand command, CancellationToken _) =>
            {
                conversation.AddUserMessage(command.Content, createdAt.AddMinutes(1));
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<BeginAssistantResponseCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((BeginAssistantResponseCommand _, CancellationToken _) =>
            {
                conversation.BeginAssistantResponse(createdAt.AddMinutes(2));
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });
        fixture.Sender
            .Setup(service => service.Send(
                It.IsAny<InterruptAssistantResponseCommand>(),
                CancellationToken.None))
            .Returns((InterruptAssistantResponseCommand _, CancellationToken _) =>
            {
                conversation.InterruptAssistantResponse(createdAt.AddMinutes(3));
                return Task.FromResult<ErrorOr<Conversation>>(conversation);
            });

        var result = await fixture.Orchestrator.StreamAsync(
            new LibraryChatCommand(null, question),
            cancellationSource.Token);
        Assert.False(result.IsError);
        var events = new List<ChatStreamEvent>();
        await foreach (var streamEvent in result.Value)
        {
            events.Add(streamEvent);
            if (streamEvent is ChatStreamEvent.Text)
            {
                cancellationSource.Cancel();
            }
        }

        Assert.Equal(cancellationSource.Token, fixture.ChatAgent.CancellationToken);
        Assert.Equal(question, conversation.Messages[1].Content);
        Assert.Equal(MessageStatus.Interrupted, conversation.Messages[^1].Status);
        Assert.Equal(string.Empty, conversation.Messages[^1].Content);
        Assert.DoesNotContain(events, streamEvent => streamEvent is ChatStreamEvent.Completed);
        Assert.Collection(
            events,
            streamEvent => Assert.IsType<ChatStreamEvent.Status>(streamEvent),
            streamEvent => Assert.Equal(new ChatStreamEvent.Text(partialAnswer), streamEvent));
        fixture.Sender.Verify(
            service => service.Send(
                It.IsAny<InterruptAssistantResponseCommand>(),
                CancellationToken.None),
            Times.Once);
        fixture.Sender.Verify(
            service => service.Send(
                It.IsAny<CompleteAssistantResponseCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class TestFixture
    {
        public TestFixture()
        {
            Orchestrator = new LibraryConversationOrchestrator(
                new LibraryChatCommandValidator(),
                Sender.Object,
                ChatAgent);
        }

        public Mock<ISender> Sender { get; } = new();

        public FakeLibraryChatAgent ChatAgent { get; } = new();

        public LibraryConversationOrchestrator Orchestrator { get; }
    }

    private sealed class FakeLibraryChatAgent : ILibraryChatAgent
    {
        public Func<IReadOnlyList<Message>, CancellationToken, IAsyncEnumerable<string>> StreamFactory
        {
            get;
            set;
        } = (_, _) => EmptyDeltasAsync();

        public Func<
            IReadOnlyList<Message>,
            Guid?,
            CancellationToken,
            IAsyncEnumerable<LibraryChatAgentUpdate>>? ToolStreamFactory { get; set; }

        public IReadOnlyList<Message>? Messages { get; private set; }

        public Guid? RequestedBookId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public IAsyncEnumerable<LibraryChatAgentUpdate> StreamAsync(
            IReadOnlyList<Message> messages,
            Guid? requestedBookId,
            CancellationToken cancellationToken)
        {
            Messages = messages;
            RequestedBookId = requestedBookId;
            CancellationToken = cancellationToken;
            return ToolStreamFactory?.Invoke(messages, requestedBookId, cancellationToken) ??
                   WrapDeltasAsync(StreamFactory(messages, cancellationToken));
        }

        private static async IAsyncEnumerable<string> EmptyDeltasAsync()
        {
            await Task.Yield();
            yield break;
        }

        private static async IAsyncEnumerable<LibraryChatAgentUpdate> WrapDeltasAsync(
            IAsyncEnumerable<string> deltas)
        {
            await foreach (var delta in deltas)
            {
                yield return new LibraryChatAgentUpdate(delta);
            }
        }
    }

    private static async IAsyncEnumerable<string> GetDeltas(params string[] deltas)
    {
        foreach (var delta in deltas)
        {
            yield return delta;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<LibraryChatAgentUpdate> GetAgentUpdates(
        params LibraryChatAgentUpdate[] updates)
    {
        foreach (var update in updates)
        {
            yield return update;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<string> GetFailedDeltas(
        string partialDelta,
        string failureMessage)
    {
        yield return partialDelta;
        await Task.Yield();
        throw new InvalidOperationException(failureMessage);
    }

    private static async IAsyncEnumerable<string> GetCancellableDeltas(
        string partialDelta,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return partialDelta;
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
