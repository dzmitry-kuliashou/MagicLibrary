using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Queries;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Queries;

public sealed class GetBookByIdQueryHandlerTests
{
    private readonly Mock<IBookReadRepository> _bookReadRepository = new();

    private GetBookByIdQueryHandler CreateHandler()
    {
        return new GetBookByIdQueryHandler(_bookReadRepository.Object);
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
    public async Task Handle_ExistingBook_ReturnsBook()
    {
        var book = CreateBook();
        _bookReadRepository
            .Setup(repository => repository.GetByIdAsync(book.Id, CancellationToken.None))
            .ReturnsAsync(book);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetBookByIdQuery(book.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Same(book, result.Value);
        _bookReadRepository.Verify(
            repository => repository.GetByIdAsync(book.Id, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MissingBook_ReturnsNotFound()
    {
        var bookId = Guid.NewGuid();
        _bookReadRepository
            .Setup(repository => repository.GetByIdAsync(bookId, CancellationToken.None))
            .ReturnsAsync((Book?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetBookByIdQuery(bookId),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        var expectedError = BookErrors.BookNotFound(bookId);
        Assert.Equal(expectedError.Type, error.Type);
        Assert.Equal(expectedError.Code, error.Code);
        _bookReadRepository.Verify(
            repository => repository.GetByIdAsync(bookId, CancellationToken.None),
            Times.Once);
    }
}
