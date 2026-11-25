using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.Requests;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Queries;

public sealed class ListBooksQueryHandlerTests
{
    private readonly Mock<IBookReadRepository> _bookReadRepository = new();

    private ListBooksQueryHandler CreateHandler()
    {
        return new ListBooksQueryHandler(_bookReadRepository.Object);
    }

    [Fact]
    public async Task Handle_ValidPage_ReturnsItemsAndPaginationMetadata()
    {
        var books = new[]
        {
            CreateBook("first.pdf"),
            CreateBook("second.pdf")
        };
        _bookReadRepository
            .Setup(repository => repository.ListAsync(1, 2, CancellationToken.None))
            .ReturnsAsync(((IReadOnlyList<Book>)books, 5));
        var handler = CreateHandler();

        var result = await handler.Handle(
            new ListBooksQuery(new ListParams { Page = "1", PageSize = "2" }),
            CancellationToken.None);

        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal(books[0].Id, item.Id);
                Assert.Equal(books[0].Title, item.Title);
                Assert.Equal(books[0].Author, item.Author);
                Assert.Equal(books[0].IndexingStatus.ToString(), item.IndexingStatus);
            },
            item =>
            {
                Assert.Equal(books[1].Id, item.Id);
                Assert.Equal(books[1].Title, item.Title);
                Assert.Equal(books[1].Author, item.Author);
                Assert.Equal(books[1].IndexingStatus.ToString(), item.IndexingStatus);
            });
        Assert.Equal(3, result.TotalPageCount);
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(2, result.PageSize);
        Assert.True(result.HasPrevious);
        Assert.True(result.HasNext);
        _bookReadRepository.Verify(
            repository => repository.ListAsync(1, 2, CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [InlineData("invalid", "2", 0, 2)]
    [InlineData("-1", "2", 0, 2)]
    [InlineData("2", "invalid", 2, 10)]
    [InlineData("2", "0", 2, 10)]
    [InlineData("2", "-1", 2, 10)]
    public async Task Handle_InvalidParameters_UsesDefaults(
        string page,
        string pageSize,
        int expectedPage,
        int expectedPageSize)
    {
        _bookReadRepository
            .Setup(repository => repository.ListAsync(
                expectedPage,
                expectedPageSize,
                CancellationToken.None))
            .ReturnsAsync(((IReadOnlyList<Book>)Array.Empty<Book>(), 0));
        var handler = CreateHandler();

        var result = await handler.Handle(
            new ListBooksQuery(new ListParams { Page = page, PageSize = pageSize }),
            CancellationToken.None);

        Assert.Equal(expectedPage, result.CurrentPage);
        Assert.Equal(expectedPageSize, result.PageSize);
        _bookReadRepository.Verify(
            repository => repository.ListAsync(
                expectedPage,
                expectedPageSize,
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LargePage_ClampsOffsetWithoutOverflow()
    {
        const int pageSize = 10;
        var expectedPage = int.MaxValue / pageSize;
        _bookReadRepository
            .Setup(repository => repository.ListAsync(
                expectedPage,
                pageSize,
                CancellationToken.None))
            .ReturnsAsync(((IReadOnlyList<Book>)Array.Empty<Book>(), 0));
        var handler = CreateHandler();

        var result = await handler.Handle(
            new ListBooksQuery(new ListParams
            {
                Page = int.MaxValue.ToString(),
                PageSize = pageSize.ToString()
            }),
            CancellationToken.None);

        Assert.Equal(expectedPage, result.CurrentPage);
        Assert.Equal(pageSize, result.PageSize);
        _bookReadRepository.Verify(
            repository => repository.ListAsync(
                expectedPage,
                pageSize,
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OutOfRangePage_ReturnsEmptyItemsAndMetadata()
    {
        _bookReadRepository
            .Setup(repository => repository.ListAsync(5, 2, CancellationToken.None))
            .ReturnsAsync(((IReadOnlyList<Book>)Array.Empty<Book>(), 5));
        var handler = CreateHandler();

        var result = await handler.Handle(
            new ListBooksQuery(new ListParams { Page = "5", PageSize = "2" }),
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.TotalPageCount);
        Assert.Equal(5, result.CurrentPage);
        Assert.Equal(2, result.PageSize);
        Assert.True(result.HasPrevious);
        Assert.False(result.HasNext);
        _bookReadRepository.Verify(
            repository => repository.ListAsync(5, 2, CancellationToken.None),
            Times.Once);
    }

    private static Book CreateBook(string sourceFileName)
    {
        return Book.Create(
            sourceFileName,
            $"Title {sourceFileName}",
            $"Author {sourceFileName}",
            $"content-hash-{sourceFileName}",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
    }
}
