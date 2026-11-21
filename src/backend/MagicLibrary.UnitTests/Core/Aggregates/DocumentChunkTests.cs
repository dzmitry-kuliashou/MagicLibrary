using MagicLibrary.Core.Aggregates;
using Xunit;

namespace MagicLibrary.UnitTests.Core.Aggregates;

public sealed class DocumentChunkTests
{
    [Fact]
    public void Create_WithoutEmbedding_RejectsValue()
    {
        Assert.ThrowsAny<ArgumentException>(() => DocumentChunk.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            0,
            1,
            "Chunk content"));
    }

    [Fact]
    public void Create_EmptyEmbedding_RejectsValue()
    {
        Assert.ThrowsAny<ArgumentException>(() => DocumentChunk.Create(
            Guid.NewGuid(),
            0,
            1,
            "Chunk content",
            [],
            "LocalProvider",
            "embedding-model:tag"));
    }

    [Fact]
    public void Create_WithEmbedding_RetainsProviderAndExactModel()
    {
        var embedding = new[] { 0.1f, 0.2f, 0.3f };

        var chunk = DocumentChunk.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            2,
            3,
            "Chunk content",
            embedding,
            "LocalProvider",
            "embedding-model:tag");

        embedding[0] = 9f;

        Assert.Equal([0.1f, 0.2f, 0.3f], chunk.Embedding);
        Assert.Equal("LocalProvider", chunk.EmbeddingProvider);
        Assert.Equal("embedding-model:tag", chunk.EmbeddingModel);
        Assert.Throws<NotSupportedException>(() => ((IList<float>)chunk.Embedding)[0] = 9f);
    }

    [Fact]
    public void Reconstitute_PreservesIdAndStoredData()
    {
        var id = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var embedding = new[] { 0.1f, 0.2f, 0.3f };

        var chunk = DocumentChunk.Reconstitute(
            id,
            bookId,
            2,
            3,
            "Chunk content",
            embedding,
            "LocalProvider",
            "embedding-model:tag");

        embedding[0] = 9f;

        Assert.Equal(id, chunk.Id);
        Assert.Equal(bookId, chunk.BookId);
        Assert.Equal(2, chunk.ChunkIndex);
        Assert.Equal(3, chunk.PageNumber);
        Assert.Equal("Chunk content", chunk.Content);
        Assert.Equal([0.1f, 0.2f, 0.3f], chunk.Embedding);
        Assert.Equal("LocalProvider", chunk.EmbeddingProvider);
        Assert.Equal("embedding-model:tag", chunk.EmbeddingModel);
    }

    [Fact]
    public void Create_DefaultBookId_RejectsValue()
    {
        Assert.ThrowsAny<ArgumentException>(() => DocumentChunk.Create(
            Guid.Empty,
            0,
            1,
            "Chunk content"));
    }

    [Fact]
    public void Create_NegativeChunkIndex_RejectsValue()
    {
        Assert.ThrowsAny<ArgumentException>(() => DocumentChunk.Create(
            Guid.NewGuid(),
            -1,
            1,
            "Chunk content"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositivePageNumber_RejectsValue(int pageNumber)
    {
        Assert.ThrowsAny<ArgumentException>(() => DocumentChunk.Create(
            Guid.NewGuid(),
            0,
            pageNumber,
            "Chunk content"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankContent_RejectsValue(string? content)
    {
        Assert.ThrowsAny<ArgumentException>(() => DocumentChunk.Create(
            Guid.NewGuid(),
            0,
            1,
            content!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_InvalidEmbeddingProvider_RejectsValue(string? provider)
    {
        Assert.ThrowsAny<ArgumentException>(() => DocumentChunk.Create(
            Guid.NewGuid(),
            0,
            1,
            "Chunk content",
            [0.1f],
            provider,
            "embedding-model:tag"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_InvalidEmbeddingModel_RejectsValue(string? model)
    {
        Assert.ThrowsAny<ArgumentException>(() => DocumentChunk.Create(
            Guid.NewGuid(),
            0,
            1,
            "Chunk content",
            [0.1f],
            "LocalProvider",
            model));
    }
}
