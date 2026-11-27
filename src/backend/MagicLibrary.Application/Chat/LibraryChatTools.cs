using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Application.Queries;
using MagicLibrary.Core.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace MagicLibrary.Application.Chat;

public sealed class LibraryChatTools(
    ISender sender,
    IEmbeddingGenerator embeddingGenerator,
    IOptions<VectorSearchOptions> vectorSearchOptions)
    : ILibraryChatTools
{
    public async Task<IReadOnlyList<Guid>> SearchBooksAsync(
        IReadOnlyList<string> terms,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SearchBooksQuery(terms), cancellationToken);
        return result.BookIds;
    }

    public async Task<IReadOnlyList<RetrievedLibraryChunk>> SearchChunksAsync(
        string query,
        IReadOnlyCollection<Guid>? bookIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var embedding = await embeddingGenerator.GenerateAsync(query, cancellationToken);
        var chunks = await sender.Send(
            new SearchDocumentChunksQuery(
                embedding,
                bookIds,
                vectorSearchOptions.Value.TopK),
            cancellationToken);

        var bookTitles = new Dictionary<Guid, string>();
        foreach (var bookId in chunks.Select(chunk => chunk.BookId).Distinct())
        {
            var bookResult = await sender.Send(new GetBookByIdQuery(bookId), cancellationToken);
            if (!bookResult.IsError)
            {
                bookTitles.Add(bookId, bookResult.Value.Title);
            }
        }

        return chunks
            .Select(chunk => new RetrievedLibraryChunk(
                chunk.Content,
                bookTitles.TryGetValue(chunk.BookId, out var bookTitle)
                    ? SourceReference.Create(
                        chunk.BookId,
                        bookTitle,
                        chunk.PageNumber,
                        chunk.Id)
                    : null))
            .ToArray();
    }
}
