using MagicLibrary.Contract.Responses;
using MagicLibrary.Core.Helpers;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Queries;

public sealed record SearchBooksQuery(IReadOnlyList<string>? Terms)
    : IRequest<SearchBooksResponse>;

public sealed class SearchBooksQueryHandler(IBookReadRepository bookReadRepository)
    : IRequestHandler<SearchBooksQuery, SearchBooksResponse>
{
    public async Task<SearchBooksResponse> Handle(
        SearchBooksQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Terms is null || request.Terms.Count == 0)
        {
            return new SearchBooksResponse(Array.Empty<Guid>());
        }

        var terms = request.Terms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .ToArray();
        if (terms.Length == 0)
        {
            return new SearchBooksResponse(Array.Empty<Guid>());
        }

        var titleIds = await bookReadRepository.SearchByTitleAsync(terms, cancellationToken);
        var authorIds = await bookReadRepository.SearchByAuthorAsync(terms, cancellationToken);
        var normalizedKeywords = terms
            .Select(KeywordNormalizer.Normalize)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .ToArray();
        var keywordIds = normalizedKeywords.Length == 0
            ? Array.Empty<Guid>()
            : await bookReadRepository.SearchByKeywordsAsync(normalizedKeywords, cancellationToken);

        var bookIds = titleIds
            .Concat(authorIds)
            .Concat(keywordIds)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        return new SearchBooksResponse(bookIds);
    }
}
