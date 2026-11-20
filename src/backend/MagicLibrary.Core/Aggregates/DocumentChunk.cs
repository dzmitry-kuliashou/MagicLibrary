using Ardalis.GuardClauses;

namespace MagicLibrary.Core.Aggregates;

public sealed class DocumentChunk : AggregateRoot
{
    private DocumentChunk(
        Guid bookId,
        int chunkIndex,
        int pageNumber,
        string content,
        float[]? embedding,
        string? embeddingProvider,
        string? embeddingModel)
    {
        BookId = bookId;
        ChunkIndex = chunkIndex;
        PageNumber = pageNumber;
        Content = content;
        Embedding = embedding is null ? null : Array.AsReadOnly(embedding);
        EmbeddingProvider = embeddingProvider;
        EmbeddingModel = embeddingModel;
    }

    public Guid BookId { get; }

    public int ChunkIndex { get; }

    public int PageNumber { get; }

    public string Content { get; }

    public IReadOnlyList<float>? Embedding { get; }

    public string? EmbeddingProvider { get; }

    public string? EmbeddingModel { get; }

    public static DocumentChunk Create(
        Guid bookId,
        int chunkIndex,
        int pageNumber,
        string content,
        IEnumerable<float>? embedding = null,
        string? embeddingProvider = null,
        string? embeddingModel = null)
    {
        Guard.Against.Default(bookId, nameof(bookId));
        Guard.Against.Negative(chunkIndex, nameof(chunkIndex));
        Guard.Against.NegativeOrZero(pageNumber, nameof(pageNumber));
        Guard.Against.NullOrWhiteSpace(content, nameof(content));
        Guard.Against.InvalidInput(
            embeddingProvider,
            nameof(embeddingProvider),
            provider => embedding is not null || provider is null,
            "Embedding provenance requires an embedding.");
        Guard.Against.InvalidInput(
            embeddingModel,
            nameof(embeddingModel),
            model => embedding is not null || model is null,
            "Embedding provenance requires an embedding.");

        if (embedding is null)
        {
            return new DocumentChunk(bookId, chunkIndex, pageNumber, content, null, null, null);
        }

        Guard.Against.NullOrWhiteSpace(embeddingProvider, nameof(embeddingProvider));
        Guard.Against.NullOrWhiteSpace(embeddingModel, nameof(embeddingModel));

        return new DocumentChunk(
            bookId,
            chunkIndex,
            pageNumber,
            content,
            embedding.ToArray(),
            embeddingProvider,
            embeddingModel);
    }
}
