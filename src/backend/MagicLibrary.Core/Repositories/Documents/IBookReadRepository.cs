using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Core.Repositories.Documents;

public interface IBookReadRepository : IDocumentReadRepository<Book>
{
    Task<Guid?> GetIdBySourceFileNameAsync(
        string sourceFileName,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> SearchByTitleAsync(
        string title,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> SearchByAuthorAsync(
        string author,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> SearchByKeywordsAsync(
        IReadOnlyCollection<string> normalizedKeywords,
        CancellationToken cancellationToken);
}
