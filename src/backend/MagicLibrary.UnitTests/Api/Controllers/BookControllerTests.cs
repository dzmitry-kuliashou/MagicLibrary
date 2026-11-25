using ErrorOr;
using MagicLibrary.Api.Controllers;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.Requests;
using MagicLibrary.Contract.Responses;
using MagicLibrary.Core.Aggregates;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Api.Controllers;

public sealed class BookControllerTests
{
    [Fact]
    public async Task List_ReturnsPaginatedResponseAndSendsQuery()
    {
        var parameters = new ListParams { Page = "2", PageSize = "3" };
        var expectedResponse = new BooksResponse
        {
            Items = Array.Empty<BookResponse>(),
            TotalPageCount = 4,
            CurrentPage = 2,
            PageSize = 3
        };
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<ListBooksQuery>(query => ReferenceEquals(query.Parameters, parameters)),
                CancellationToken.None))
            .ReturnsAsync(expectedResponse);
        var controller = new BookController(sender.Object);

        var result = await controller.List(parameters, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expectedResponse, response.Value);
        sender.Verify(
            service => service.Send(
                It.Is<ListBooksQuery>(query => ReferenceEquals(query.Parameters, parameters)),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task GetById_ExistingBook_ReturnsBookResponse()
    {
        var book = Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetBookByIdQuery>(query => query.Id == book.Id),
                CancellationToken.None))
            .ReturnsAsync((ErrorOr<Book>)book);
        var controller = new BookController(sender.Object);

        var result = await controller.GetById(book.Id, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        var responseBody = Assert.IsType<BookResponse>(response.Value);
        Assert.Equal(book.Id, responseBody.Id);
        Assert.Equal(book.Title, responseBody.Title);
        Assert.Equal(book.Author, responseBody.Author);
        Assert.Equal(book.IndexingStatus.ToString(), responseBody.IndexingStatus);
        sender.Verify(
            service => service.Send(
                It.Is<GetBookByIdQuery>(query => query.Id == book.Id),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task GetById_MissingBook_ReturnsNotFound()
    {
        var bookId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetBookByIdQuery>(query => query.Id == bookId),
                CancellationToken.None))
            .ReturnsAsync((ErrorOr<Book>)BookErrors.BookNotFound(bookId));
        var controller = new BookController(sender.Object);

        var result = await controller.GetById(bookId, CancellationToken.None);

        var response = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, response.StatusCode);
        sender.Verify(
            service => service.Send(
                It.Is<GetBookByIdQuery>(query => query.Id == bookId),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Search_ReturnsBookIdsResponseAndSendsTerms()
    {
        var terms = new[] { "title", "keyword" };
        var expectedResponse = new SearchBooksResponse([Guid.NewGuid(), Guid.NewGuid()]);
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<SearchBooksQuery>(query => ReferenceEquals(query.Terms, terms)),
                CancellationToken.None))
            .ReturnsAsync(expectedResponse);
        var controller = new BookController(sender.Object);

        var result = await controller.Search(
            new SearchBooksRequest(terms),
            CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expectedResponse, response.Value);
        sender.Verify(
            service => service.Send(
                It.Is<SearchBooksQuery>(query => ReferenceEquals(query.Terms, terms)),
                CancellationToken.None),
            Times.Once);
    }
}
