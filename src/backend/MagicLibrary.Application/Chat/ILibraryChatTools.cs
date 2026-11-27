using MagicLibrary.Core.ValueObjects;

namespace MagicLibrary.Application.Chat;

public interface ILibraryChatTools
{
    Task<IReadOnlyList<Guid>> SearchBooksAsync(
        IReadOnlyList<string> terms,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RetrievedLibraryChunk>> SearchChunksAsync(
        string query,
        IReadOnlyCollection<Guid>? bookIds,
        CancellationToken cancellationToken);
}

public sealed record RetrievedLibraryChunk(
    string Content,
    SourceReference? SourceReference);
