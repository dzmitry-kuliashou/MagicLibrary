using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MagicLibrary.IntegrationTests.Repositories.Documents;

[Collection(IntegrationTestCollection.Name)]
public sealed class IndexingJobRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;
    private readonly IIndexingJobReadRepository _readRepository;
    private readonly IIndexingJobWriteRepository _writeRepository;

    public IndexingJobRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _serviceProvider = fixture.CreateServiceProvider();
        _scope = _serviceProvider.CreateAsyncScope();
        _readRepository = _scope.ServiceProvider.GetRequiredService<IIndexingJobReadRepository>();
        _writeRepository = _scope.ServiceProvider.GetRequiredService<IIndexingJobWriteRepository>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task AnyRunningAsync_TracksStoredJobStatus()
    {
        var job = IndexingJob.Create(null);

        await _writeRepository.CreateAsync(job, CancellationToken.None);
        Assert.False(await _readRepository.AnyRunningAsync(CancellationToken.None));

        job.Start(DateTimeOffset.UtcNow);
        await _writeRepository.UpdateAsync(job, CancellationToken.None);
        Assert.True(await _readRepository.AnyRunningAsync(CancellationToken.None));

        job.Complete(DateTimeOffset.UtcNow);
        await _writeRepository.UpdateAsync(job, CancellationToken.None);
        Assert.False(await _readRepository.AnyRunningAsync(CancellationToken.None));
    }
}
