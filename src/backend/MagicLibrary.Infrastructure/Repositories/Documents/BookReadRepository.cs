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

    public async Task<Guid?> GetIdBySourceFileNameAsync(
        string sourceFileName,
        CancellationToken cancellationToken)
    {
        var ids = await _querySession.Query<BookDocument>()
            .Where(document => document.SourceFileName.Equals(
                sourceFileName,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(document => document.Id)
            .Select(document => document.Id)
            .Take(1)
            .ToListAsync(cancellationToken);

        return ids.Count == 0 ? null : ids[0];
    }

    public async Task<IReadOnlyList<Guid>> SearchByTitleAsync(
        string title,
        CancellationToken cancellationToken)
    {
        return await _querySession.Query<BookDocument>()
            .Where(document => document.Title.Contains(title, StringComparison.OrdinalIgnoreCase))
            .OrderBy(document => document.Id)
            .Select(document => document.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> SearchByAuthorAsync(
        string author,
        CancellationToken cancellationToken)
    {
        return await _querySession.Query<BookDocument>()
            .Where(document => document.Author.Contains(author, StringComparison.OrdinalIgnoreCase))
            .OrderBy(document => document.Id)
            .Select(document => document.Id)
            .ToListAsync(cancellationToken);
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
