using MagicLibrary.Api.Results;
using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.Requests;
using MagicLibrary.Contract.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MagicLibrary.Api.Controllers;

[ApiController]
[Route("conversations")]
public sealed class ConversationController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ConversationsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ListParams parameters,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new ListConversationsQuery(parameters), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetConversationByIdQuery(id), cancellationToken);

        return result.ToActionResult(
            conversation => Ok(new ConversationResponse(
                conversation.Id,
                conversation.Name,
                conversation.BookId,
                conversation.CreatedAt,
                conversation.UpdatedAt,
                conversation.Messages
                    .Select(message => new ConversationMessageResponse(
                        message.Role.ToString(),
                        message.Status.ToString(),
                        message.Content,
                        message.SourceReferences
                            .Select(sourceReference => new ConversationSourceReferenceResponse(
                                sourceReference.BookId,
                                sourceReference.BookTitle,
                                sourceReference.PageNumber,
                                sourceReference.ChunkId))
                            .ToArray()))
                    .ToArray())));
    }
}
