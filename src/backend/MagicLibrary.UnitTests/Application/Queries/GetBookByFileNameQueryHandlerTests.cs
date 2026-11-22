using ErrorOr;
using MagicLibrary.Application.Queries;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Queries;

public sealed class GetBookByFileNameQueryHandlerTests
{
    private readonly Mock<IBookReadRepository> _readRepository = new();

    private GetBookByFileNameQueryHandler CreateHandler()
    {
        return new GetBookByFileNameQueryHandler(_readRepository.Object);
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

    [Fact]
    public async Task Handle_BookDoesNotExist_ReturnsNotFound()
    {
        const string filename = "missing.pdf";
        _readRepository
            .Setup(repository => repository.FindByFileName(filename, CancellationToken.None))
            .ReturnsAsync((Book?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetBookByFileNameQuery(filename),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("Book.NotFound", error.Code);
    }

    [Fact]
    public async Task Handle_BookExists_ReturnsBook()
    {
        var book = CreateBook();
        _readRepository
            .Setup(repository => repository.FindByFileName(book.SourceFileName, CancellationToken.None))
            .ReturnsAsync(book);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetBookByFileNameQuery(book.SourceFileName),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Same(book, result.Value);
    }
}
