using Microsoft.Extensions.DependencyInjection;

namespace MagicLibrary.Application.Indexing;

public sealed class IndexingJobBackgroundProcessor(
    IServiceScopeFactory serviceScopeFactory)
    : IIndexingJobBackgroundProcessor
{
    public void Start(Guid indexingJobId)
    {
        _ = Task.Run(() => ProcessAsync(indexingJobId));
    }

    private async Task ProcessAsync(Guid indexingJobId)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var worker = scope.ServiceProvider.GetRequiredService<IndexingWorker>();
            await worker.ProcessAsync(indexingJobId, CancellationToken.None);
        }
        catch (Exception)
        {
            // TODO: Log unexpected failures when application logging is configured.
            throw;
        }
    }
}
