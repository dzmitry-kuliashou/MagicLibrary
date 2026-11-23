using MagicLibrary.Api.Results;
using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.IndexingJobs.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MagicLibrary.Api.Controllers;

[ApiController]
[Route("indexing-jobs")]
public sealed class IndexingJobController(ISender sender) : ControllerBase
{
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
