using MagicLibrary.Core.Aggregates;
using MagicLibrary.Infrastructure.StorageModels.Vectors.Mappers;
using Xunit;

namespace MagicLibrary.UnitTests.Infrastructure.StorageModels.Vectors;

public sealed class DocumentChunkMapperTests
{
    [Fact]
    public void MapToRowAndBack_DocumentChunk_PreservesCurrentState()
    {
        var original = DocumentChunk.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            2,
            3,
            "Chunk content",
            [0.1f, 0.2f, 0.3f],
            "LocalProvider",
            "embedding-model:tag");

        var row = DocumentChunkMapper.ToRow(original);
        var restored = DocumentChunkMapper.ToAggregate(row);

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.BookId, restored.BookId);
        Assert.Equal(original.ChunkIndex, restored.ChunkIndex);
        Assert.Equal(original.PageNumber, restored.PageNumber);
        Assert.Equal(original.Content, restored.Content);
        Assert.Equal(original.Embedding, restored.Embedding);
        Assert.Equal(original.EmbeddingProvider, restored.EmbeddingProvider);
        Assert.Equal(original.EmbeddingModel, restored.EmbeddingModel);
    }
}
