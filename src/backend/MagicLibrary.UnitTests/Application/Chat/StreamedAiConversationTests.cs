using System.Runtime.CompilerServices;
using ErrorOr;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.Conversations;
using MagicLibrary.Core.Aggregates;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Chat;

public sealed class StreamedAiConversationTests
{
    [Fact]
    public async Task StreamAsync_CompletedStream_InvokesSuccessWithFullContentBeforeCompletionEvent()
    {
        var conversationId = Guid.NewGuid();
        string? completedContent = null;
        var events = new List<ChatStreamEvent>();
        var streamedConversation = new TestStreamedAiConversation();

        await foreach (var streamEvent in streamedConversation.Run(
            conversationId,
            GetDeltas("Answer", " complete."),
            (content, _) =>
            {
                completedContent = content;
                return Task.FromResult<ErrorOr<Conversation>>(CreateConversation());
            },
            (_, _) => throw new Xunit.Sdk.XunitException("The failure callback should not run."),
            _ => throw new Xunit.Sdk.XunitException("The interruption callback should not run."),
            CancellationToken.None))
        {
            if (streamEvent is ChatStreamEvent.Completed)
            {
                Assert.Equal("Answer complete.", completedContent);
            }

            events.Add(streamEvent);
        }

        Assert.Collection(
            events,
            streamEvent => Assert.Equal(new ChatStreamEvent.Status(conversationId), streamEvent),
            streamEvent => Assert.Equal(new ChatStreamEvent.Text("Answer"), streamEvent),
            streamEvent => Assert.Equal(new ChatStreamEvent.Text(" complete."), streamEvent),
            streamEvent => Assert.IsType<ChatStreamEvent.Completed>(streamEvent));
    }

    [Fact]
    public async Task StreamAsync_FailedStream_InvokesFailureAndEmitsErrorWithoutSuccess()
    {
        var failureCallbackWasCalled = false;
        var successCallbackWasCalled = false;
        var events = new List<ChatStreamEvent>();
        var streamedConversation = new TestStreamedAiConversation();

        await foreach (var streamEvent in streamedConversation.Run(
            Guid.NewGuid(),
            GetFailedDeltas(),
            (_, _) =>
            {
                successCallbackWasCalled = true;
                return Task.FromResult<ErrorOr<Conversation>>(CreateConversation());
            },
            (exception, _) =>
            {
                failureCallbackWasCalled = true;
                Assert.Equal("Generation failed.", exception.Message);
                return Task.FromResult<ErrorOr<Conversation>>(CreateConversation());
            },
            _ => throw new Xunit.Sdk.XunitException("The interruption callback should not run."),
            CancellationToken.None))
        {
            events.Add(streamEvent);
        }

        Assert.True(failureCallbackWasCalled);
        Assert.False(successCallbackWasCalled);
        Assert.Collection(
            events,
            streamEvent => Assert.IsType<ChatStreamEvent.Status>(streamEvent),
            streamEvent => Assert.Equal(new ChatStreamEvent.Text("Partial answer"), streamEvent),
            streamEvent => Assert.Equal(new ChatStreamEvent.Error("Generation failed."), streamEvent));
    }

    [Fact]
    public async Task StreamAsync_CancelledStream_InvokesInterruptionWithoutCompletion()
    {
        using var cancellationSource = new CancellationTokenSource();
        var interruptionCallbackWasCalled = false;
        var successCallbackWasCalled = false;
        var failureCallbackWasCalled = false;
        var events = new List<ChatStreamEvent>();
        var streamedConversation = new TestStreamedAiConversation();

        await foreach (var streamEvent in streamedConversation.Run(
            Guid.NewGuid(),
            GetCancellableDeltas(),
            (_, _) =>
            {
                successCallbackWasCalled = true;
                return Task.FromResult<ErrorOr<Conversation>>(CreateConversation());
            },
            (_, _) =>
            {
                failureCallbackWasCalled = true;
                return Task.FromResult<ErrorOr<Conversation>>(CreateConversation());
            },
            token =>
            {
                interruptionCallbackWasCalled = true;
                Assert.False(token.CanBeCanceled);
                return Task.FromResult<ErrorOr<Conversation>>(CreateConversation());
            },
            cancellationSource.Token))
        {
            events.Add(streamEvent);
            if (streamEvent is ChatStreamEvent.Text)
            {
                cancellationSource.Cancel();
            }
        }

        Assert.True(interruptionCallbackWasCalled);
        Assert.False(successCallbackWasCalled);
        Assert.False(failureCallbackWasCalled);
        Assert.Collection(
            events,
            streamEvent => Assert.IsType<ChatStreamEvent.Status>(streamEvent),
            streamEvent => Assert.Equal(new ChatStreamEvent.Text("Partial answer"), streamEvent));
    }

    private sealed class TestStreamedAiConversation : StreamedAiConversation
    {
        public IAsyncEnumerable<ChatStreamEvent> Run(
            Guid conversationId,
            IAsyncEnumerable<string> deltas,
            Func<string, CancellationToken, Task<ErrorOr<Conversation>>> onSuccess,
            Func<Exception, CancellationToken, Task<ErrorOr<Conversation>>> onFailed,
            Func<CancellationToken, Task<ErrorOr<Conversation>>> onInterrupted,
            CancellationToken cancellationToken) =>
            StreamAsync(
                conversationId,
                deltas,
                onSuccess,
                onFailed,
                onInterrupted,
                cancellationToken);
    }

    private static async IAsyncEnumerable<string> GetDeltas(params string[] deltas)
    {
        foreach (var delta in deltas)
        {
            yield return delta;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<string> GetFailedDeltas()
    {
        yield return "Partial answer";
        await Task.Yield();
        throw new InvalidOperationException("Generation failed.");
    }

    private static async IAsyncEnumerable<string> GetCancellableDeltas(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "Partial answer";
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private static Conversation CreateConversation() =>
        Conversation.Create("Research", DateTimeOffset.UtcNow, "System instructions");
}
