using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Core.Repositories.Documents;

public interface IBookReadRepository : IDocumentReadRepository<Book>
{
    Task<bool> BookExistsAsync(
        string sourceFileName,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> SearchByTitleAsync(
        IReadOnlyCollection<string> titles,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> SearchByAuthorAsync(
        IReadOnlyCollection<string> authors,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> SearchByKeywordsAsync(
        IReadOnlyCollection<string> normalizedKeywords,
        CancellationToken cancellationToken);
}
