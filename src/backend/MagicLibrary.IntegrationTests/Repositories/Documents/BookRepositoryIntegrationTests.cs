using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Exceptions;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MagicLibrary.IntegrationTests.Repositories.Documents;

[Collection(IntegrationTestCollection.Name)]
public sealed class BookRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;
    private readonly IBookReadRepository _readRepository;
    private readonly IBookWriteRepository _writeRepository;

    public BookRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _serviceProvider = fixture.CreateServiceProvider();
        _scope = _serviceProvider.CreateAsyncScope();
        _readRepository = _scope.ServiceProvider.GetRequiredService<IBookReadRepository>();
        _writeRepository = _scope.ServiceProvider.GetRequiredService<IBookWriteRepository>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingAndUnknownIds_ReturnsStoredBookAndNull()
    {
        var book = CreateBook();
        await _writeRepository.CreateAsync(book, CancellationToken.None);

        var found = await _readRepository.GetByIdAsync(book.Id, CancellationToken.None);
        var missing = await _readRepository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(book.Id, found.Id);
        Assert.Equal(book.Title, found.Title);
        Assert.Null(missing);
    }

    [Fact]
    public async Task SearchByTitleAsync_CaseInsensitivePartialMatch_ReturnsMatchingIds()
    {
        var matchingBook = CreateBook("The Pragmatic Programmer");
        var otherBook = CreateBook("A Different Topic");
        await _writeRepository.CreateAsync(matchingBook, CancellationToken.None);
        await _writeRepository.CreateAsync(otherBook, CancellationToken.None);

        var matchingIds = await _readRepository.SearchByTitleAsync(
            "pRaGmAtIc",
            CancellationToken.None);

        Assert.Equal([matchingBook.Id], matchingIds);
    }

    [Fact]
    public async Task SearchByAuthorAsync_CaseInsensitivePartialMatch_ReturnsMatchingIds()
    {
        var matchingBook = CreateBook(author: "Ada Lovelace");
        var otherBook = CreateBook(author: "Grace Hopper");
        await _writeRepository.CreateAsync(matchingBook, CancellationToken.None);
        await _writeRepository.CreateAsync(otherBook, CancellationToken.None);

        var matchingIds = await _readRepository.SearchByAuthorAsync(
            "lOvElAcE",
            CancellationToken.None);

        Assert.Equal([matchingBook.Id], matchingIds);
    }

    [Fact]
    public async Task GetIdBySourceFileNameAsync_CaseInsensitiveExactMatch_ReturnsIdOrNull()
    {
        var book = Book.Create(
            "A Curious File.PDF",
            "Title",
            "Author",
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UnixEpoch);
        await _writeRepository.CreateAsync(book, CancellationToken.None);

        var caseVariant = await _readRepository.GetIdBySourceFileNameAsync(
            "a curious file.pdf",
            CancellationToken.None);
        var partialName = await _readRepository.GetIdBySourceFileNameAsync(
            "curious file",
            CancellationToken.None);
        var unknownName = await _readRepository.GetIdBySourceFileNameAsync(
            "unknown.pdf",
            CancellationToken.None);

        Assert.Equal(book.Id, caseVariant);
        Assert.Null(partialName);
        Assert.Null(unknownName);
    }

    [Fact]
    public async Task SearchByKeywordsAsync_ExactAnyMatch_ReturnsMatchingIds()
    {
        var matchingBook = CreateBook();
        matchingBook.AddKeywords(["semantic search!!", "rag"]);
        var otherBook = CreateBook();
        otherBook.AddKeywords(["semantic_search_extra"]);
        await _writeRepository.CreateAsync(matchingBook, CancellationToken.None);
        await _writeRepository.CreateAsync(otherBook, CancellationToken.None);

        var matchingIds = await _readRepository.SearchByKeywordsAsync(
            ["semantic_search", "unrelated"],
            CancellationToken.None);
        var partialMatches = await _readRepository.SearchByKeywordsAsync(
            ["semantic"],
            CancellationToken.None);
        var emptySearch = await _readRepository.SearchByKeywordsAsync(
            [],
            CancellationToken.None);

        Assert.Equal([matchingBook.Id], matchingIds);
        Assert.Empty(partialMatches);
        Assert.Empty(emptySearch);
    }

    [Fact]
    public async Task ListAsync_WithSkipAndTake_ReturnsBooksOrderedById()
    {
        var existingBooks = await _readRepository.ListAsync(0, int.MaxValue, CancellationToken.None);
        var createdBooks = await CreateBooksAsync(3, CancellationToken.None);
        var expectedIds = existingBooks
            .Concat(createdBooks)
            .OrderBy(book => book.Id.ToString("N"), StringComparer.Ordinal)
            .Skip(1)
            .Take(2)
            .Select(book => book.Id)
            .ToArray();

        var page = await _readRepository.ListAsync(1, 2, CancellationToken.None);

        Assert.Equal(expectedIds, page.Select(book => book.Id));
    }

    [Fact]
    public async Task CreateAsync_NewBook_PersistsBookForReadback()
    {
        var book = CreateBook();
        book.AddKeywords(["integration", "postgres"]);

        await _writeRepository.CreateAsync(book, CancellationToken.None);
        var persistedBook = await _readRepository.GetByIdAsync(book.Id, CancellationToken.None);

        Assert.NotNull(persistedBook);
        Assert.Equal(book.Id, persistedBook.Id);
        Assert.Equal(book.SourceFileName, persistedBook.SourceFileName);
        Assert.Equal(book.Title, persistedBook.Title);
        Assert.Equal(book.Author, persistedBook.Author);
        Assert.Equal(book.Keywords, persistedBook.Keywords);
        Assert.Equal(book.ContentHash, persistedBook.ContentHash);
        Assert.Equal(book.AddedAt, persistedBook.AddedAt);
        Assert.Equal(book.IndexedAt, persistedBook.IndexedAt);
        Assert.Equal(book.IndexingStatus, persistedBook.IndexingStatus);
    }

    [Fact]
    public async Task UpdateAsync_ExistingBook_PersistsUpdatedMetadata()
    {
        var book = CreateBook();
        await _writeRepository.CreateAsync(book, CancellationToken.None);
        book.UpdateMetadata("Updated title", "Updated author");

        await _writeRepository.UpdateAsync(book, CancellationToken.None);
        var persistedBook = await _readRepository.GetByIdAsync(book.Id, CancellationToken.None);

        Assert.NotNull(persistedBook);
        Assert.Equal("Updated title", persistedBook.Title);
        Assert.Equal("Updated author", persistedBook.Author);
    }

    [Fact]
    public async Task CreateAsync_DuplicateId_ThrowsAggregateAlreadyExistsException()
    {
        var book = CreateBook();
        await _writeRepository.CreateAsync(book, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AggregateAlreadyExistsException>(
            () => _writeRepository.CreateAsync(book, CancellationToken.None));

        Assert.Equal(book.Id, exception.AggregateId);
    }

    [Fact]
    public async Task CreateAsync_DuplicateSourceFileNameIgnoringCase_ThrowsAggregateAlreadyExistsException()
    {
        var existingBook = Book.Create(
            "SharedName.pdf",
            "Existing title",
            "Existing author",
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UnixEpoch);
        var duplicateBook = Book.Create(
            "sharedname.PDF",
            "Duplicate title",
            "Duplicate author",
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UnixEpoch);
        await _writeRepository.CreateAsync(existingBook, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AggregateAlreadyExistsException>(
            () => _writeRepository.CreateAsync(duplicateBook, CancellationToken.None));

        Assert.Equal(duplicateBook.Id, exception.AggregateId);
    }

    [Fact]
    public async Task UpdateAsync_UnknownBook_ThrowsAggregateNotFoundException()
    {
        var book = CreateBook();

        var exception = await Assert.ThrowsAsync<AggregateNotFoundException>(
            () => _writeRepository.UpdateAsync(book, CancellationToken.None));

        Assert.Equal(book.Id, exception.AggregateId);
    }

    private static Book CreateBook(string? title = null, string? author = null)
    {
        var uniqueValue = Guid.NewGuid().ToString("N");
        return Book.Create(
            $"{uniqueValue}.pdf",
            title ?? $"Book {uniqueValue}",
            author ?? $"Author {uniqueValue}",
            uniqueValue,
            DateTimeOffset.UnixEpoch);
    }

    private async Task<IReadOnlyList<Book>> CreateBooksAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var books = Enumerable.Range(0, count)
            .Select(_ => CreateBook())
            .ToArray();

        foreach (var book in books)
        {
            await _writeRepository.CreateAsync(book, cancellationToken);
        }

        return books;
    }
}
