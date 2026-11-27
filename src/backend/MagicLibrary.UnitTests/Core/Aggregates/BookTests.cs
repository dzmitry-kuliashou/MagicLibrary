using MagicLibrary.Core.Aggregates;
using Xunit;

namespace MagicLibrary.UnitTests.Core.Aggregates;

public sealed class BookTests
{
    [Fact]
    public void Create_ValidMetadata_InitializesBook()
    {
        var addedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var book = Book.Create("book.pdf", "Title", "Author", "content-hash", addedAt);

        Assert.NotEqual(Guid.Empty, book.Id);
        Assert.Equal("book.pdf", book.SourceFileName);
        Assert.Equal("Title", book.Title);
        Assert.Equal("Author", book.Author);
        Assert.Empty(book.Keywords);
        Assert.Equal("content-hash", book.ContentHash);
        Assert.Equal(addedAt, book.AddedAt);
        Assert.Null(book.IndexedAt);
        Assert.Equal(BookIndexingStatus.Pending, book.IndexingStatus);
    }

    [Fact]
    public void Reconstitute_PersistedState_RestoresAllFields()
    {
        var id = Guid.NewGuid();
        var addedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5));
        var indexedAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.FromHours(-5));

        var book = Book.Reconstitute(
            id,
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            addedAt,
            ["semantic", "search"],
            indexedAt,
            BookIndexingStatus.Indexed);

        Assert.Equal(id, book.Id);
        Assert.Equal("book.pdf", book.SourceFileName);
        Assert.Equal("Title", book.Title);
        Assert.Equal("Author", book.Author);
        Assert.Equal(2, book.Keywords.Count);
        Assert.Contains("semantic", book.Keywords);
        Assert.Contains("search", book.Keywords);
        Assert.Equal("content-hash", book.ContentHash);
        Assert.Equal(addedAt, book.AddedAt);
        Assert.Equal(addedAt.Offset, book.AddedAt.Offset);
        Assert.Equal(indexedAt, book.IndexedAt);
        Assert.Equal(indexedAt.Offset, book.IndexedAt!.Value.Offset);
        Assert.Equal(BookIndexingStatus.Indexed, book.IndexingStatus);
    }

    [Fact]
    public void UpdateMetadata_ValidValues_UpdatesTitleAndAuthor()
    {
        var book = CreateBook();

        book.UpdateMetadata("Updated title", "Updated author");

        Assert.Equal("Updated title", book.Title);
        Assert.Equal("Updated author", book.Author);
    }

    [Fact]
    public void AddKeywords_NormalizedDuplicates_MergesUniqueValues()
    {
        var book = CreateBook();

        book.AddKeywords(["semantic", "search"]);
        book.AddKeywords([" SEMANTIC ", "rag", "Search!!"]);

        Assert.Equal(3, book.Keywords.Count);
        Assert.Contains("semantic", book.Keywords);
        Assert.Contains("search", book.Keywords);
        Assert.Contains("rag", book.Keywords);
    }

    [Fact]
    public void Keywords_ReadOnlyView_CannotMutateBook()
    {
        var book = CreateBook();
        book.AddKeywords(["semantic"]);
        var keywords = book.Keywords;

        Assert.Throws<NotSupportedException>(() => ((IList<string>)keywords).Add("search"));

        Assert.Single(book.Keywords);
        Assert.Contains("semantic", book.Keywords);
    }

    [Fact]
    public void MarkIndexingStarted_ValidTransition_UpdatesLatestStatus()
    {
        var book = CreateBook();

        book.MarkIndexingStarted();

        Assert.Equal(BookIndexingStatus.InProgress, book.IndexingStatus);
        Assert.Null(book.IndexedAt);
    }

    [Theory]
    [InlineData(BookIndexingStatus.Pending)]
    [InlineData(BookIndexingStatus.InProgress)]
    [InlineData(BookIndexingStatus.Indexed)]
    [InlineData(BookIndexingStatus.Failed)]
    public void ResetIndexing_AnyPriorStatus_ClearsIndexingState(BookIndexingStatus status)
    {
        var book = CreateBook();
        var indexedAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

        switch (status)
        {
            case BookIndexingStatus.Pending:
                break;
            case BookIndexingStatus.InProgress:
                book.MarkIndexingStarted();
                break;
            case BookIndexingStatus.Indexed:
                book.MarkIndexingStarted();
                book.MarkIndexed(indexedAt);
                break;
            case BookIndexingStatus.Failed:
                book.MarkIndexingFailed();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        book.AddKeywords(["semantic", "search"]);

        book.ResetIndexing();

        Assert.Equal(BookIndexingStatus.Pending, book.IndexingStatus);
        Assert.Empty(book.Keywords);
        Assert.Null(book.IndexedAt);
    }

    [Theory]
    [InlineData(BookIndexingStatus.InProgress)]
    [InlineData(BookIndexingStatus.Indexed)]
    [InlineData(BookIndexingStatus.Failed)]
    public void MarkIndexingStarted_InvalidTransition_RejectsOperation(BookIndexingStatus status)
    {
        var book = CreateBook();

        switch (status)
        {
            case BookIndexingStatus.InProgress:
                book.MarkIndexingStarted();
                break;
            case BookIndexingStatus.Indexed:
                book.MarkIndexingStarted();
                book.MarkIndexed(new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero));
                break;
            case BookIndexingStatus.Failed:
                book.MarkIndexingStarted();
                book.MarkIndexingFailed();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        var initialStatus = book.IndexingStatus;
        var initialIndexedAt = book.IndexedAt;

        Assert.Throws<ArgumentException>(() => book.MarkIndexingStarted());

        Assert.Equal(initialStatus, book.IndexingStatus);
        Assert.Equal(initialIndexedAt, book.IndexedAt);
    }

    [Fact]
    public void MarkIndexed_CompletedAt_UpdatesStatusAndTimestamp()
    {
        var book = CreateBook();
        var indexedAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

        book.MarkIndexingStarted();
        book.MarkIndexed(indexedAt);

        Assert.Equal(BookIndexingStatus.Indexed, book.IndexingStatus);
        Assert.Equal(indexedAt, book.IndexedAt);
    }

    [Theory]
    [InlineData(BookIndexingStatus.Pending)]
    [InlineData(BookIndexingStatus.Indexed)]
    [InlineData(BookIndexingStatus.Failed)]
    public void MarkIndexed_InvalidTransition_RejectsOperation(BookIndexingStatus status)
    {
        var book = CreateBook();

        switch (status)
        {
            case BookIndexingStatus.Pending:
                break;
            case BookIndexingStatus.Indexed:
                book.MarkIndexingStarted();
                book.MarkIndexed(new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero));
                break;
            case BookIndexingStatus.Failed:
                book.MarkIndexingStarted();
                book.MarkIndexingFailed();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        var initialStatus = book.IndexingStatus;
        var initialIndexedAt = book.IndexedAt;

        Assert.Throws<ArgumentException>(() =>
            book.MarkIndexed(new DateTimeOffset(2026, 2, 4, 4, 5, 6, TimeSpan.Zero)));

        Assert.Equal(initialStatus, book.IndexingStatus);
        Assert.Equal(initialIndexedAt, book.IndexedAt);
    }

    [Fact]
    public void MarkIndexingFailed_ValidTransition_UpdatesLatestStatus()
    {
        var book = CreateBook();

        book.MarkIndexingFailed();

        Assert.Equal(BookIndexingStatus.Failed, book.IndexingStatus);
        Assert.Null(book.IndexedAt);
    }

    [Theory]
    [InlineData(BookIndexingStatus.Indexed)]
    [InlineData(BookIndexingStatus.Failed)]
    public void MarkIndexingFailed_InvalidTransition_RejectsOperation(BookIndexingStatus status)
    {
        var book = CreateBook();

        switch (status)
        {
            case BookIndexingStatus.Pending:
                break;
            case BookIndexingStatus.Indexed:
                book.MarkIndexingStarted();
                book.MarkIndexed(new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero));
                break;
            case BookIndexingStatus.Failed:
                book.MarkIndexingStarted();
                book.MarkIndexingFailed();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        var initialStatus = book.IndexingStatus;
        var initialIndexedAt = book.IndexedAt;

        Assert.Throws<ArgumentException>(() => book.MarkIndexingFailed());

        Assert.Equal(initialStatus, book.IndexingStatus);
        Assert.Equal(initialIndexedAt, book.IndexedAt);
    }

    [Theory]
    [InlineData("", "Title", "Author", "content-hash")]
    [InlineData(" ", "Title", "Author", "content-hash")]
    [InlineData(null, "Title", "Author", "content-hash")]
    [InlineData("book.pdf", "", "Author", "content-hash")]
    [InlineData("book.pdf", " ", "Author", "content-hash")]
    [InlineData("book.pdf", null, "Author", "content-hash")]
    [InlineData("book.pdf", "Title", null, "content-hash")]
    [InlineData("book.pdf", "Title", "Author", "")]
    [InlineData("book.pdf", "Title", "Author", " ")]
    [InlineData("book.pdf", "Title", "Author", null)]
    public void Create_BlankRequiredText_RejectsValue(
        string? sourceFileName,
        string? title,
        string? author,
        string? contentHash)
    {
        Assert.ThrowsAny<ArgumentException>(() => Book.Create(
            sourceFileName!,
            title!,
            author!,
            contentHash!,
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
    }

    [Fact]
    public void Create_DefaultAddedAt_RejectsValue()
    {
        Assert.Throws<ArgumentException>(() => Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            default));
    }

    [Fact]
    public void Create_BlankAuthor_AllowsMissingMetadata()
    {
        var book = Book.Create(
            "book.pdf",
            "Title",
            "",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

        Assert.Equal("", book.Author);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void UpdateMetadata_BlankTitle_RejectsValue(string? title)
    {
        var book = CreateBook();

        Assert.ThrowsAny<ArgumentException>(() => book.UpdateMetadata(title!, "Updated author"));

        Assert.Equal("Title", book.Title);
        Assert.Equal("Author", book.Author);
    }

    [Fact]
    public void UpdateMetadata_NullAuthor_RejectsValueWithoutChangingMetadata()
    {
        var book = CreateBook();

        Assert.Throws<ArgumentNullException>(() => book.UpdateMetadata("Updated title", null!));

        Assert.Equal("Title", book.Title);
        Assert.Equal("Author", book.Author);
    }

    [Fact]
    public void AddKeywords_NullCollection_RejectsValue()
    {
        var book = CreateBook();

        Assert.Throws<ArgumentNullException>(() => book.AddKeywords(null!));

        Assert.Empty(book.Keywords);
    }

    [Fact]
    public void MarkIndexed_DefaultTimestamp_RejectsValue()
    {
        var book = CreateBook();

        Assert.Throws<ArgumentException>(() => book.MarkIndexed(default));

        Assert.Equal(BookIndexingStatus.Pending, book.IndexingStatus);
        Assert.Null(book.IndexedAt);
    }

    private static Book CreateBook()
    {
        return Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
    }
}
