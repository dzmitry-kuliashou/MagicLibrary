using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Core.ValueObjects;
using MagicLibrary.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MagicLibrary.IntegrationTests.Repositories.Documents;

[Collection(IntegrationTestCollection.Name)]
public sealed class ConversationRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;
    private readonly IConversationReadRepository _readRepository;
    private readonly IConversationWriteRepository _writeRepository;

    public ConversationRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _serviceProvider = fixture.CreateServiceProvider();
        _scope = _serviceProvider.CreateAsyncScope();
        _readRepository = _scope.ServiceProvider.GetRequiredService<IConversationReadRepository>();
        _writeRepository = _scope.ServiceProvider.GetRequiredService<IConversationWriteRepository>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task CreateAndReadBack_PreservesMessageStatus()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var conversation = Conversation.Create("Status round-trip", createdAt, "System instructions");
        conversation.AddUserMessage("First question", createdAt.AddSeconds(1));
        conversation.BeginAssistantResponse(createdAt.AddSeconds(2));
        conversation.CompleteAssistantResponse("First answer", [], createdAt.AddSeconds(3));
        conversation.AddUserMessage("Second question", createdAt.AddSeconds(4));
        conversation.BeginAssistantResponse(createdAt.AddSeconds(5));
        conversation.FailAssistantResponse(createdAt.AddSeconds(6));
        conversation.AddUserMessage("Third question", createdAt.AddSeconds(7));
        conversation.BeginAssistantResponse(createdAt.AddSeconds(8));
        conversation.InterruptAssistantResponse(createdAt.AddSeconds(9));
        conversation.AddUserMessage("Fourth question", createdAt.AddSeconds(10));
        conversation.BeginAssistantResponse(createdAt.AddSeconds(11));

        await _writeRepository.CreateAsync(conversation, CancellationToken.None);
        var persistedConversation = await _readRepository.GetByIdAsync(
            conversation.Id,
            CancellationToken.None);

        Assert.NotNull(persistedConversation);
        Assert.Equal(
            [
                MessageStatus.Completed,
                MessageStatus.Failed,
                MessageStatus.Interrupted,
                MessageStatus.Pending
            ],
            persistedConversation.Messages
                .Where(message => message.Role == MessageRole.Assistant)
                .Select(message => message.Status));
    }
}
