using MagicLibrary.Api.Results;
using MagicLibrary.Application.Commands;
using MagicLibrary.Contract.Books.Requests;
using MagicLibrary.Contract.Books.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MagicLibrary.Api.Controllers;

[ApiController]
[Route("books")]
public sealed class BookController(ISender sender) : ControllerBase
{
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
            book => Ok(new BookResponse(book.Id, book.Title, book.Author)));
    }
}
