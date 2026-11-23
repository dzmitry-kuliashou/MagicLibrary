using ErrorOr;

namespace MagicLibrary.Application.Errors;

public static class DocumentChunkErrors
{
    public static Error InvalidBookId =>
        Error.Validation("DocumentChunks.InvalidBookId", "A non-default book ID is required.");

    public static Error MissingChunks =>
        Error.Validation("DocumentChunks.MissingChunks", "At least one document chunk is required.");

    public static Error InvalidChunkIndex =>
        Error.Validation("DocumentChunks.InvalidChunkIndex", "Chunk index must be zero or greater.");

    public static Error InvalidPageNumber =>
        Error.Validation("DocumentChunks.InvalidPageNumber", "Page number must be greater than zero.");

    public static Error MissingContent =>
        Error.Validation("DocumentChunks.MissingContent", "Chunk content is required.");

    public static Error MissingEmbedding =>
        Error.Validation("DocumentChunks.MissingEmbedding", "A nonempty embedding is required.");

    public static Error InvalidEmbeddingDimensions(int expectedDimensions) =>
        Error.Validation(
            "DocumentChunks.InvalidEmbeddingDimensions",
            $"Embedding must contain exactly {expectedDimensions} values.");
}
