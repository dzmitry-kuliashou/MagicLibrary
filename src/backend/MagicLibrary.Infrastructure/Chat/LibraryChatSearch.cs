using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Application.Queries;
using MagicLibrary.Core.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace MagicLibrary.Infrastructure.Chat;

public sealed class LibraryChatSearch(
    ISender sender,
    IEmbeddingGenerator embeddingGenerator,
    IOptions<VectorSearchOptions> vectorSearchOptions,
    IBookSearchTermExtractor bookSearchTermExtractor)
    : ILibraryChatSearch
{
    public async Task<IReadOnlyList<Guid>> SearchBooksAsync(
        string latestUserMessage,
        CancellationToken cancellationToken)
    {
        var searchTerms = await bookSearchTermExtractor.ExtractAsync(
            latestUserMessage,
            cancellationToken);
        if (!searchTerms.HasSearchTerms)
        {
            return [];
        }

        var terms = searchTerms.ExactTitleTerms
            .Concat(searchTerms.ExactAuthorTerms)
            .Concat(searchTerms.KeywordPhrases)
            .ToArray();

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
