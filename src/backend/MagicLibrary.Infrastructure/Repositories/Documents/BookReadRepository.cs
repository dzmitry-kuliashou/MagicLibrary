using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using MagicLibrary.Infrastructure.StorageModels.Documents.Mappers;
using Marten;

namespace MagicLibrary.Infrastructure.Repositories.Documents;

internal sealed class BookReadRepository(IQuerySession querySession)
    : DocumentReadRepository<Book, BookDocument>(querySession), IBookReadRepository
{
    private readonly IQuerySession _querySession = querySession;

    protected override Book ToAggregate(BookDocument document) =>
        BookDocumentMapper.ToAggregate(document);

    public Task<bool> BookExistsAsync(
        string sourceFileName,
        CancellationToken cancellationToken)
    {
        return _querySession.Query<BookDocument>()
            .Where(document => document.SourceFileName.Equals(
                sourceFileName,
                StringComparison.OrdinalIgnoreCase))
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> SearchByTitleAsync(
        IReadOnlyCollection<string> titles,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(titles);

        var searchTerms = titles
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (searchTerms.Length == 0)
        {
            return Array.Empty<Guid>();
        }

        var matchingIds = new HashSet<Guid>();
        foreach (var title in searchTerms)
        {
            var ids = await _querySession.Query<BookDocument>()
                .Where(document => document.Title.Contains(title, StringComparison.OrdinalIgnoreCase))
                .OrderBy(document => document.Id)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken);

            matchingIds.UnionWith(ids);
        }

        return matchingIds.OrderBy(id => id).ToList();
    }

    public async Task<IReadOnlyList<Guid>> SearchByAuthorAsync(
        IReadOnlyCollection<string> authors,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authors);

        var searchTerms = authors
            .Where(author => !string.IsNullOrWhiteSpace(author))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (searchTerms.Length == 0)
        {
            return Array.Empty<Guid>();
        }

        var matchingIds = new HashSet<Guid>();
        foreach (var author in searchTerms)
        {
            var ids = await _querySession.Query<BookDocument>()
                .Where(document => document.Author.Contains(author, StringComparison.OrdinalIgnoreCase))
                .OrderBy(document => document.Id)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken);

            matchingIds.UnionWith(ids);
        }

        return matchingIds.OrderBy(id => id).ToList();
    }

    public async Task<IReadOnlyList<Guid>> SearchByKeywordsAsync(
        IReadOnlyCollection<string> normalizedKeywords,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(normalizedKeywords);
        if (normalizedKeywords.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var matchingIds = new HashSet<Guid>();
        foreach (var keyword in normalizedKeywords.Distinct(StringComparer.Ordinal))
        {
            var ids = await _querySession.Query<BookDocument>()
                .Where(document => document.Keywords.Contains(keyword))
                .OrderBy(document => document.Id)
                .Select(document => document.Id)
                .ToListAsync(cancellationToken);

            matchingIds.UnionWith(ids);
        }

        return matchingIds.OrderBy(id => id).ToList();
    }
}
