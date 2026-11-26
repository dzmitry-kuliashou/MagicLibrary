using MagicLibrary.Application;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Application.Indexing;
using MagicLibrary.Application.KeywordExtraction;
using MagicLibrary.Application.PdfProcessing;
using MagicLibrary.Core.Repositories.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersCallableIndexingServices()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddLogging();
        services.AddSingleton<IOptions<BookStorageOptions>>(
            Options.Create(new BookStorageOptions()));
        services.AddSingleton<IBookReadRepository>(Mock.Of<IBookReadRepository>());
        services.AddSingleton<IIndexingJobReadRepository>(Mock.Of<IIndexingJobReadRepository>());
        services.AddSingleton<IPdfTextExtractor>(Mock.Of<IPdfTextExtractor>());
        services.AddSingleton<IEmbeddingGenerator>(Mock.Of<IEmbeddingGenerator>());
        services.AddSingleton<IKeywordExtractor>(Mock.Of<IKeywordExtractor>());

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        Assert.NotNull(scopedServices.GetRequiredService<IndexingWorker>());
        Assert.NotNull(scopedServices.GetRequiredService<IndexingScenarioFactory>());
        Assert.NotNull(scopedServices.GetRequiredService<TimeProvider>());

        var scenarios = scopedServices.GetServices<IIndexingScenario>().ToArray();
        Assert.Equal(2, scenarios.Length);
        Assert.Contains(scenarios, scenario => scenario is LibraryIndexingScenario);
        Assert.Contains(scenarios, scenario => scenario is BookIndexingScenario);
    }
}
