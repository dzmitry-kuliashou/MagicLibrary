using System.Runtime.CompilerServices;
using MagicLibrary.Core.ValueObjects;
using MagicLibrary.Infrastructure.Chat;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Infrastructure.Chat;

public sealed class LibraryChatAgentTests
{
    [Fact]
    public async Task StreamAsync_EmitsTextDeltasFromProviderChunks()
    {
        var (agent, _) = CreateAgent(
            (_, _, _) => GetUpdates("The ", "", "answer."));
        Message[] messages =
        [
            Message.Create(MessageRole.User, "Question.", [])
        ];
        var deltas = new List<string>();

        await foreach (var delta in agent.StreamAsync(messages, CancellationToken.None))
        {
            deltas.Add(delta);
        }

        Assert.Equal(["The ", "answer."], deltas);
    }

    [Fact]
    public async Task StreamAsync_PassesConversationHistoryToConfiguredClient()
    {
        IReadOnlyList<ChatMessage>? receivedMessages = null;
        ChatOptions? receivedOptions = new();
        var (agent, _) = CreateAgent(
            (messages, options, _) =>
            {
                receivedMessages = messages.ToArray();
                receivedOptions = options;
                return GetUpdates("Answer");
            });
        Message[] history =
        [
            Message.Create(MessageRole.System, "System instructions.", []),
            Message.Create(MessageRole.User, "Earlier question.", []),
            Message.Create(MessageRole.Assistant, "Earlier answer.", []),
            Message.Create(MessageRole.User, "Current question.", [])
        ];
        var deltas = new List<string>();

        await foreach (var delta in agent.StreamAsync(history, CancellationToken.None))
        {
            deltas.Add(delta);
        }

        Assert.Collection(
            receivedMessages!,
            message =>
            {
                Assert.Equal(ChatRole.System, message.Role);
                Assert.Equal("System instructions.", message.Text);
            },
            message =>
            {
                Assert.Equal(ChatRole.User, message.Role);
                Assert.Equal("Earlier question.", message.Text);
            },
            message =>
            {
                Assert.Equal(ChatRole.Assistant, message.Role);
                Assert.Equal("Earlier answer.", message.Text);
            },
            message =>
            {
                Assert.Equal(ChatRole.User, message.Role);
                Assert.Equal("Current question.", message.Text);
            });
        Assert.Null(receivedOptions);
        Assert.Equal(["Answer"], deltas);
    }

    [Fact]
    public async Task StreamAsync_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        CancellationToken? receivedToken = null;
        var (agent, client) = CreateAgent(
            (_, _, cancellationToken) =>
            {
                receivedToken = cancellationToken;
                return GetCancellableUpdates(cancellationToken);
            });
        Message[] messages =
        [
            Message.Create(MessageRole.User, "Question.", [])
        ];
        await using var updates = agent
            .StreamAsync(messages, cancellationSource.Token)
            .GetAsyncEnumerator();

        Assert.True(await updates.MoveNextAsync());
        Assert.Equal("Partial answer", updates.Current);
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => { await updates.MoveNextAsync(); });

        Assert.Equal(cancellationSource.Token, receivedToken);
        client.Verify(
            service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                cancellationSource.Token),
            Times.Once);
    }

    [Fact]
    public async Task StreamAsync_InvalidOrFailedResponseThrows()
    {
        var expectedException = new InvalidOperationException("Provider stream failed.");
        var (agent, client) = CreateAgent(
            (_, _, _) => GetFaultingUpdates());
        Message[] messages =
        [
            Message.Create(MessageRole.User, "Question.", [])
        ];
        var deltas = new List<string>();

        async IAsyncEnumerable<ChatResponseUpdate> GetFaultingUpdates()
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Partial answer");
            await Task.Yield();
            throw expectedException;
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
            await foreach (var delta in agent.StreamAsync(messages, CancellationToken.None))
            {
                deltas.Add(delta);
            }
            });

        Assert.Same(expectedException, exception);
        Assert.Equal(["Partial answer"], deltas);
        client.Verify(
            service => service.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions?>(),
            CancellationToken.None),
            Times.Once);
    }

    private static (
        LibraryChatAgent Agent,
        Mock<IChatClient> Client) CreateAgent(
        Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken,
            IAsyncEnumerable<ChatResponseUpdate>> updateFactory)
    {
        var client = new Mock<IChatClient>();
        client
            .Setup(service => service.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? options,
                    CancellationToken cancellationToken) =>
                updateFactory(messages, options, cancellationToken));

        return (new LibraryChatAgent(client.Object), client);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> GetUpdates(params string[] deltas)
    {
        foreach (var delta in deltas)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, delta);
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> GetCancellableUpdates(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "Partial answer");
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
