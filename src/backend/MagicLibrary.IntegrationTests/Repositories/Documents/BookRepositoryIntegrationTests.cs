using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Exceptions;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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
    public async Task SearchByTitleAsync_MultipleCaseInsensitiveTerms_ReturnsUniqueIdsInOrder()
    {
        var token = Guid.NewGuid().ToString("N");
        var pragmaticBook = CreateBook($"{token} Pragmatic Programmer");
        var designBook = CreateBook($"Domain Design {token}");
        var overlappingBook = CreateBook($"{token} Pragmatic Design {token}");
        var otherBook = CreateBook($"{token} Different Topic");
        await _writeRepository.CreateAsync(pragmaticBook, CancellationToken.None);
        await _writeRepository.CreateAsync(designBook, CancellationToken.None);
        await _writeRepository.CreateAsync(overlappingBook, CancellationToken.None);
        await _writeRepository.CreateAsync(otherBook, CancellationToken.None);

        var matchingIds = await _readRepository.SearchByTitleAsync(
            [$"{token} pRaG", $"dEsIgN {token}"],
            CancellationToken.None);
        var expectedIds = new[] { pragmaticBook.Id, designBook.Id, overlappingBook.Id }
            .OrderBy(id => id)
            .ToArray();

        Assert.Equal(expectedIds, matchingIds);
    }

    [Fact]
    public async Task SearchByAuthorAsync_MultipleCaseInsensitiveTerms_ReturnsUniqueIdsInOrder()
    {
        var token = Guid.NewGuid().ToString("N");
        var adaBook = CreateBook(author: $"Ada {token} Lovelace");
        var hopperBook = CreateBook(author: $"Grace Hopper {token}");
        var overlappingBook = CreateBook(author: $"Ada {token} Hopper {token}");
        var otherBook = CreateBook(author: $"Other {token} Author");
        await _writeRepository.CreateAsync(adaBook, CancellationToken.None);
        await _writeRepository.CreateAsync(hopperBook, CancellationToken.None);
        await _writeRepository.CreateAsync(overlappingBook, CancellationToken.None);
        await _writeRepository.CreateAsync(otherBook, CancellationToken.None);

        var matchingIds = await _readRepository.SearchByAuthorAsync(
            [$"aDa {token}", $"hOpPeR {token}"],
            CancellationToken.None);
        var expectedIds = new[] { adaBook.Id, hopperBook.Id, overlappingBook.Id }
            .OrderBy(id => id)
            .ToArray();

        Assert.Equal(expectedIds, matchingIds);
    }

    [Fact]
    public async Task SearchByTitleAsync_NullEmptyAndBlankTerms_RejectsNullAndIgnoresBlanks()
    {
        var token = Guid.NewGuid().ToString("N");
        var book = CreateBook($"Title {token}");
        var unrelatedBook = CreateBook($"Unrelated {token}");
        await _writeRepository.CreateAsync(book, CancellationToken.None);
        await _writeRepository.CreateAsync(unrelatedBook, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _readRepository.SearchByTitleAsync(null!, CancellationToken.None));
        var emptySearch = await _readRepository.SearchByTitleAsync(
            [],
            CancellationToken.None);
        var blankMembersSearch = await _readRepository.SearchByTitleAsync(
            [" ", $"title {token}", "\t"],
            CancellationToken.None);
        var allBlankSearch = await _readRepository.SearchByTitleAsync(
            ["", " ", "\t"],
            CancellationToken.None);

        Assert.Equal("titles", exception.ParamName);
        Assert.Empty(emptySearch);
        Assert.Equal([book.Id], blankMembersSearch);
        Assert.Empty(allBlankSearch);
    }

    [Fact]
    public async Task SearchByAuthorAsync_NullEmptyAndBlankTerms_RejectsNullAndIgnoresBlanks()
    {
        var token = Guid.NewGuid().ToString("N");
        var book = CreateBook(author: $"Author {token}");
        var unrelatedBook = CreateBook(author: $"Unrelated {token}");
        await _writeRepository.CreateAsync(book, CancellationToken.None);
        await _writeRepository.CreateAsync(unrelatedBook, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _readRepository.SearchByAuthorAsync(null!, CancellationToken.None));
        var emptySearch = await _readRepository.SearchByAuthorAsync(
            [],
            CancellationToken.None);
        var blankMembersSearch = await _readRepository.SearchByAuthorAsync(
            [" ", $"author {token}", "\t"],
            CancellationToken.None);
        var allBlankSearch = await _readRepository.SearchByAuthorAsync(
            ["", " ", "\t"],
            CancellationToken.None);

        Assert.Equal("authors", exception.ParamName);
        Assert.Empty(emptySearch);
        Assert.Equal([book.Id], blankMembersSearch);
        Assert.Empty(allBlankSearch);
    }

    [Fact]
    public async Task BookSearchIndexes_AfterMartenSchemaInitialization_CreatesExpectedGinIndexes()
    {
        await _writeRepository.CreateAsync(CreateBook(), CancellationToken.None);

        var indexNames = new[]
        {
            "idx_book_title_trgm",
            "idx_book_author_trgm",
            "idx_book_keywords_gin"
        };
        var trigramIndexNames = indexNames[..2];
        var dataSource = _scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand("""
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE indexname = ANY (@indexNames)
            """);
        command.Parameters.AddWithValue("indexNames", indexNames);

        var indexDefinitions = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                indexDefinitions.Add(reader.GetString(0), reader.GetString(1));
            }
        }

        Assert.Equal(indexNames.Length, indexDefinitions.Count);
        foreach (var indexName in indexNames)
        {
            var definition = indexDefinitions[indexName];
            Assert.True(definition.Contains("USING gin", StringComparison.OrdinalIgnoreCase));
        }

        foreach (var indexName in trigramIndexNames)
        {
            var definition = indexDefinitions[indexName];
            Assert.True(definition.Contains("gin_trgm_ops", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task BookExistsAsync_CaseInsensitiveExactMatch_ReturnsTrueOrFalse()
    {
        var book = Book.Create(
            "A Curious File.PDF",
            "Title",
            "Author",
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UnixEpoch);
        await _writeRepository.CreateAsync(book, CancellationToken.None);

        var caseVariant = await _readRepository.BookExistsAsync(
            "a curious file.pdf",
            CancellationToken.None);
        var partialName = await _readRepository.BookExistsAsync(
            "curious file",
            CancellationToken.None);
        var unknownName = await _readRepository.BookExistsAsync(
            "unknown.pdf",
            CancellationToken.None);

        Assert.True(caseVariant);
        Assert.False(partialName);
        Assert.False(unknownName);
    }

    [Fact]
    public async Task FindByFileName_CaseInsensitiveMatch_ReturnsStoredBook()
    {
        var book = CreateBook();
        await _writeRepository.CreateAsync(book, CancellationToken.None);

        var found = await _readRepository.FindByFileName(
            book.SourceFileName.ToUpperInvariant(),
            CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(book.Id, found.Id);
        Assert.Equal(book.SourceFileName, found.SourceFileName);
    }

    [Fact]
    public async Task FindByFileName_UnknownFilename_ReturnsNull()
    {
        var found = await _readRepository.FindByFileName(
            $"missing-{Guid.NewGuid():N}.pdf",
            CancellationToken.None);

        Assert.Null(found);
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
    public async Task ListAsync_WithPageAndPageSize_ReturnsOrderedPageAndTotalCount()
    {
        var createdBooks = await CreateBooksAsync(3, CancellationToken.None);
        var allBooks = await _readRepository.ListAsync(0, int.MaxValue, CancellationToken.None);
        var expectedIds = allBooks.Items
            .Skip(2)
            .Take(2)
            .Select(book => book.Id)
            .ToArray();

        var page = await _readRepository.ListAsync(1, 2, CancellationToken.None);

        Assert.Equal(expectedIds, page.Items.Select(book => book.Id));
        Assert.Equal(allBooks.Items.Count, page.TotalCount);
        Assert.Equal(allBooks.TotalCount, page.TotalCount);
        Assert.All(createdBooks, book => Assert.Contains(allBooks.Items, listedBook => listedBook.Id == book.Id));
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
