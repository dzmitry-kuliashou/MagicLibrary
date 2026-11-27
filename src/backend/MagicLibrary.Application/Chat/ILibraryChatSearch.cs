using MagicLibrary.Core.ValueObjects;

namespace MagicLibrary.Application.Chat;

public interface ILibraryChatSearch
{
    Task<IReadOnlyList<Guid>> SearchBooksAsync(
        string latestUserMessage,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RetrievedLibraryChunk>> SearchChunksAsync(
        string query,
        IReadOnlyCollection<Guid>? bookIds,
        CancellationToken cancellationToken);
}

public sealed record RetrievedLibraryChunk(
    string Content,
    SourceReference? SourceReference);
