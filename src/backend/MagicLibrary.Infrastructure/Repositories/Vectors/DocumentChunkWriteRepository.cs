using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Vectors;
using MagicLibrary.Infrastructure.StorageModels.Vectors.Mappers;
using Npgsql;

namespace MagicLibrary.Infrastructure.Repositories.Vectors;

internal sealed class DocumentChunkWriteRepository(NpgsqlDataSource dataSource) : IDocumentChunkWriteRepository
{
    public async Task ReplaceAsync(
        Guid scopeId,
        IReadOnlyCollection<DocumentChunk> records,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Any(record => record.BookId != scopeId))
        {
            throw new ArgumentException("All document chunks must belong to the specified scope.", nameof(records));
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = new NpgsqlCommand(
            "DELETE FROM document_chunks WHERE book_id = $1",
            connection,
            transaction))
        {
            deleteCommand.Parameters.AddWithValue(scopeId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var record in records)
        {
            var row = DocumentChunkMapper.ToRow(record);
            await using var insertCommand = new NpgsqlCommand(
                """
                INSERT INTO document_chunks
                    (id, book_id, chunk_index, page_number, content, embedding, embedding_provider, embedding_model)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                """,
                connection,
                transaction);

            insertCommand.Parameters.AddWithValue(row.Id);
            insertCommand.Parameters.AddWithValue(row.BookId);
            insertCommand.Parameters.AddWithValue(row.ChunkIndex);
            insertCommand.Parameters.AddWithValue(row.PageNumber);
            insertCommand.Parameters.AddWithValue(row.Content);
            insertCommand.Parameters.AddWithValue(row.Embedding);
            insertCommand.Parameters.AddWithValue(row.EmbeddingProvider);
            insertCommand.Parameters.AddWithValue(row.EmbeddingModel);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
