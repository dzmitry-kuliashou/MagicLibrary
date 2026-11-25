using MagicLibrary.Api.Results;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.Requests;
using MagicLibrary.Contract.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MagicLibrary.Api.Controllers;

[ApiController]
[Route("books")]
public sealed class BookController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(BooksResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ListParams parameters,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new ListBooksQuery(parameters), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBookByIdQuery(id), cancellationToken);

        return result.ToActionResult(
            book => Ok(new BookResponse(
                book.Id,
                book.Title,
                book.Author,
                book.IndexingStatus.ToString())));
    }

    [HttpPost("books:search")]
    [ProducesResponseType(typeof(SearchBooksResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromBody] SearchBooksRequest request,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new SearchBooksQuery(request.Terms), cancellationToken);
        return Ok(response);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(BookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(
        Guid id,
        [FromBody] UpdateBookMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateBookMetadataCommand(id, request.Title, request.Author),
            cancellationToken);

        return result.ToActionResult(
            book => Ok(new BookResponse(
                book.Id,
                book.Title,
                book.Author,
                book.IndexingStatus.ToString())));
    }
}
