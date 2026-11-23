using MagicLibrary.Application.Indexing;
using MagicLibrary.Core.Aggregates;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Indexing;

public sealed class IndexingScenarioFactoryTests
{
    [Fact]
    public void GetScenario_LibraryWideJob_ReturnsCapableScenario()
    {
        var job = IndexingJob.Create(null);
        var libraryScenario = new Mock<IIndexingScenario>();
        var bookScenario = new Mock<IIndexingScenario>();
        libraryScenario
            .Setup(scenario => scenario.CanProcess(It.IsAny<IndexingJob>()))
            .Returns((IndexingJob candidate) => candidate.BookId is null);
        bookScenario
            .Setup(scenario => scenario.CanProcess(It.IsAny<IndexingJob>()))
            .Returns((IndexingJob candidate) => candidate.BookId is not null);
        var factory = new IndexingScenarioFactory(
            [libraryScenario.Object, bookScenario.Object]);

        var selectedScenario = factory.GetScenario(job);

        Assert.Same(libraryScenario.Object, selectedScenario);
    }

    [Fact]
    public void GetScenario_BookSpecificJob_ReturnsCapableScenario()
    {
        var job = IndexingJob.Create(Guid.NewGuid());
        var libraryScenario = new Mock<IIndexingScenario>();
        var bookScenario = new Mock<IIndexingScenario>();
        libraryScenario
            .Setup(scenario => scenario.CanProcess(It.IsAny<IndexingJob>()))
            .Returns((IndexingJob candidate) => candidate.BookId is null);
        bookScenario
            .Setup(scenario => scenario.CanProcess(It.IsAny<IndexingJob>()))
            .Returns((IndexingJob candidate) => candidate.BookId is not null);
        var factory = new IndexingScenarioFactory(
            [libraryScenario.Object, bookScenario.Object]);

        var selectedScenario = factory.GetScenario(job);

        Assert.Same(bookScenario.Object, selectedScenario);
    }
}
