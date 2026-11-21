using Pgvector;

namespace MagicLibrary.Infrastructure.StorageModels.Vectors;

public sealed class DocumentChunkRow
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public int ChunkIndex { get; set; }

    public int PageNumber { get; set; }

    public string Content { get; set; } = string.Empty;

    public Vector Embedding { get; set; } = new(Array.Empty<float>());

    public string EmbeddingProvider { get; set; } = string.Empty;

    public string EmbeddingModel { get; set; } = string.Empty;
}
