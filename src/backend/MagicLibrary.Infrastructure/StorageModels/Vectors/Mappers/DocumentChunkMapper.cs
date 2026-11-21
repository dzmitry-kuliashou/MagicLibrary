using MagicLibrary.Core.Aggregates;
using Pgvector;

namespace MagicLibrary.Infrastructure.StorageModels.Vectors.Mappers;

internal static class DocumentChunkMapper
{
    internal static DocumentChunkRow ToRow(DocumentChunk chunk)
    {
        return new DocumentChunkRow
        {
            Id = chunk.Id,
            BookId = chunk.BookId,
            ChunkIndex = chunk.ChunkIndex,
            PageNumber = chunk.PageNumber,
            Content = chunk.Content,
            Embedding = new Vector(chunk.Embedding.ToArray()),
            EmbeddingProvider = chunk.EmbeddingProvider,
            EmbeddingModel = chunk.EmbeddingModel
        };
    }

    internal static DocumentChunk ToAggregate(DocumentChunkRow row)
    {
        return DocumentChunk.Reconstitute(
            row.Id,
            row.BookId,
            row.ChunkIndex,
            row.PageNumber,
            row.Content,
            row.Embedding.ToArray(),
            row.EmbeddingProvider,
            row.EmbeddingModel);
    }
}
