using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Vectors;
using MagicLibrary.Infrastructure.StorageModels.Vectors;
using MagicLibrary.Infrastructure.StorageModels.Vectors.Mappers;
using Npgsql;
using Pgvector;

namespace MagicLibrary.Infrastructure.Repositories.Vectors;

internal sealed class DocumentChunkReadRepository(NpgsqlDataSource dataSource) : IDocumentChunkReadRepository
{
    private const string SearchSql = """
        SELECT id, book_id, chunk_index, page_number, content, embedding, embedding_provider, embedding_model
        FROM document_chunks
        ORDER BY embedding <=> $1
        LIMIT $2
        """;

    private const string SearchByBooksSql = """
        SELECT id, book_id, chunk_index, page_number, content, embedding, embedding_provider, embedding_model
        FROM document_chunks
        WHERE book_id = ANY($1)
        ORDER BY embedding <=> $2
        LIMIT $3
        """;

    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        IReadOnlyList<float> queryEmbedding,
        int topK,
        IReadOnlyCollection<Guid>? bookIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);
        if (queryEmbedding.Count == 0)
        {
            throw new ArgumentException("Query embedding must not be empty.", nameof(queryEmbedding));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);
        var hasBookScope = bookIds is { Count: > 0 };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            hasBookScope ? SearchByBooksSql : SearchSql,
            connection);

        if (bookIds is { Count: > 0 } scopedBookIds)
        {
            command.Parameters.AddWithValue(scopedBookIds.ToArray());
        }

        command.Parameters.AddWithValue(new Vector(queryEmbedding.ToArray()));
        command.Parameters.AddWithValue(topK);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<DocumentChunk>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new DocumentChunkRow
            {
                Id = reader.GetGuid(0),
                BookId = reader.GetGuid(1),
                ChunkIndex = reader.GetInt32(2),
                PageNumber = reader.GetInt32(3),
                Content = reader.GetString(4),
                Embedding = reader.GetFieldValue<Vector>(5),
                EmbeddingProvider = reader.GetString(6),
                EmbeddingModel = reader.GetString(7)
            };
            results.Add(DocumentChunkMapper.ToAggregate(row));
        }

        return results;
    }
}
