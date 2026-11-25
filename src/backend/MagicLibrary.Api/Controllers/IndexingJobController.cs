using MagicLibrary.Api.Results;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.Requests;
using MagicLibrary.Contract.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MagicLibrary.Api.Controllers;

[ApiController]
[Route("indexing-jobs")]
public sealed class IndexingJobController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(RunIndexingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Run(
        RunIndexingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RunIndexingCommand(request.BookId),
            cancellationToken);

        return result.ToActionResult(indexingJobId =>
            Accepted(new RunIndexingResponse(indexingJobId)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IndexingJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetIndexingJobByIdQuery(id), cancellationToken);

        return result.ToActionResult(
            job => Ok(new IndexingJobResponse(
                job.Id,
                job.BookId,
                job.Status.ToString(),
                job.Progress,
                job.StartedAt,
                job.CompletedAt,
                job.FailureMessage,
                job.FileErrors
                    .Select(error => new IndexingJobFileErrorResponse(
                        error.FileName,
                        error.Message))
                    .ToArray())));
    }
}
