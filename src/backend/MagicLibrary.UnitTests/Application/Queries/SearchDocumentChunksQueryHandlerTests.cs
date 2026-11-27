using MagicLibrary.Application.Queries;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Vectors;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Queries;

public sealed class SearchDocumentChunksQueryHandlerTests
{
    private readonly Mock<IDocumentChunkReadRepository> _repository = new();

    private SearchDocumentChunksQueryHandler CreateHandler()
    {
        return new SearchDocumentChunksQueryHandler(_repository.Object);
    }

    [Fact]
    public async Task Handle_ForwardsEmbeddingBookIdsAndTopK()
    {
        var embedding = new[] { 0.1f, 0.2f, 0.3f };
        var bookIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        const int topK = 7;
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        IReadOnlyList<DocumentChunk> expectedChunks = Array.Empty<DocumentChunk>();
        _repository
            .Setup(repository => repository.SearchAsync(
                It.Is<IReadOnlyList<float>>(values => values.SequenceEqual(embedding)),
                topK,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(bookIds)),
                cancellationToken))
            .ReturnsAsync(expectedChunks);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SearchDocumentChunksQuery(embedding, bookIds, topK),
            cancellationToken);

        Assert.Same(expectedChunks, result);
        _repository.Verify(
            repository => repository.SearchAsync(
                It.Is<IReadOnlyList<float>>(values => values.SequenceEqual(embedding)),
                topK,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(bookIds)),
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyBookIds_SearchesWithoutBookScope()
    {
        var embedding = new[] { 0.1f, 0.2f, 0.3f };
        IReadOnlyCollection<Guid> bookIds = Array.Empty<Guid>();
        const int topK = 5;
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        IReadOnlyList<DocumentChunk> expectedChunks = Array.Empty<DocumentChunk>();
        _repository
            .Setup(repository => repository.SearchAsync(
                It.Is<IReadOnlyList<float>>(values => values.SequenceEqual(embedding)),
                topK,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0),
                cancellationToken))
            .ReturnsAsync(expectedChunks);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SearchDocumentChunksQuery(embedding, bookIds, topK),
            cancellationToken);

        Assert.Same(expectedChunks, result);
        _repository.Verify(
            repository => repository.SearchAsync(
                It.Is<IReadOnlyList<float>>(values => values.SequenceEqual(embedding)),
                topK,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0),
                cancellationToken),
            Times.Once);
    }
}
