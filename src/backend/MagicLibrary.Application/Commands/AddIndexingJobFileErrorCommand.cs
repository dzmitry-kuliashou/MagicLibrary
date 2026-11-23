using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record AddIndexingJobFileErrorCommand(
    Guid IndexingJobId,
    string FileName,
    string Message) : IRequest<ErrorOr<IndexingJob>>;

public sealed class AddIndexingJobFileErrorCommandHandler(
    IIndexingJobReadRepository indexingJobReadRepository,
    IIndexingJobWriteRepository indexingJobWriteRepository)
    : IRequestHandler<AddIndexingJobFileErrorCommand, ErrorOr<IndexingJob>>
{
    public async Task<ErrorOr<IndexingJob>> Handle(
        AddIndexingJobFileErrorCommand request,
        CancellationToken cancellationToken)
    {
        var indexingJob = await indexingJobReadRepository.GetByIdAsync(
            request.IndexingJobId,
            cancellationToken);
        if (indexingJob is null)
        {
            return IndexingJobErrors.IndexingJobNotFound(request.IndexingJobId);
        }

        if (indexingJob.Status != IndexingJobStatus.Running)
        {
            return IndexingJobErrors.FileErrorUpdateNotAllowed(indexingJob.Status);
        }

        indexingJob.AddFileError(request.FileName, request.Message);
        return await indexingJobWriteRepository.UpdateAsync(indexingJob, cancellationToken);
    }
}
