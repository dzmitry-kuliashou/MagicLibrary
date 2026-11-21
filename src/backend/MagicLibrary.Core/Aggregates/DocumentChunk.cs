using Ardalis.GuardClauses;

namespace MagicLibrary.Core.Aggregates;

public sealed class DocumentChunk : AggregateRoot
{
    private DocumentChunk(
        Guid bookId,
        int chunkIndex,
        int pageNumber,
        string content,
        float[] embedding,
        string embeddingProvider,
        string embeddingModel)
        : this(Guid.NewGuid(), bookId, chunkIndex, pageNumber, content, embedding, embeddingProvider, embeddingModel)
    {
    }

    private DocumentChunk(
        Guid id,
        Guid bookId,
        int chunkIndex,
        int pageNumber,
        string content,
        float[] embedding,
        string embeddingProvider,
        string embeddingModel)
        : base(id)
    {
        BookId = bookId;
        ChunkIndex = chunkIndex;
        PageNumber = pageNumber;
        Content = content;
        Embedding = Array.AsReadOnly(embedding);
        EmbeddingProvider = embeddingProvider;
        EmbeddingModel = embeddingModel;
    }

    public Guid BookId { get; }

    public int ChunkIndex { get; }

    public int PageNumber { get; }

    public string Content { get; }

    public IReadOnlyList<float> Embedding { get; }

    public string EmbeddingProvider { get; }

    public string EmbeddingModel { get; }

    public static DocumentChunk Create(
        Guid bookId,
        int chunkIndex,
        int pageNumber,
        string content,
        IEnumerable<float>? embedding = null,
        string? embeddingProvider = null,
        string? embeddingModel = null)
    {
        Guard.Against.Default(bookId);
        Guard.Against.Negative(chunkIndex);
        Guard.Against.NegativeOrZero(pageNumber);
        Guard.Against.NullOrWhiteSpace(content);
        Guard.Against.Null(embedding);
        var embeddingValues = embedding.ToArray();
        Guard.Against.InvalidInput(
            embeddingValues,
            nameof(embedding),
            values => values.Length > 0,
            "Embedding must not be empty.");
        Guard.Against.NullOrWhiteSpace(embeddingProvider);
        Guard.Against.NullOrWhiteSpace(embeddingModel);

        return new DocumentChunk(
            bookId,
            chunkIndex,
            pageNumber,
            content,
            embeddingValues,
            embeddingProvider,
            embeddingModel);
    }

    internal static DocumentChunk Reconstitute(
        Guid id,
        Guid bookId,
        int chunkIndex,
        int pageNumber,
        string content,
        IEnumerable<float> embedding,
        string embeddingProvider,
        string embeddingModel)
    {
        // Persisted state is trusted; write-side APIs validate new aggregates.
        return new DocumentChunk(
            id,
            bookId,
            chunkIndex,
            pageNumber,
            content,
            embedding.ToArray(),
            embeddingProvider,
            embeddingModel);
    }
}
