using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record UpdateIndexingJobProgressCommand(Guid IndexingJobId, string Progress)
    : IRequest<ErrorOr<IndexingJob>>;

public sealed class UpdateIndexingJobProgressCommandHandler(
    IIndexingJobReadRepository indexingJobReadRepository,
    IIndexingJobWriteRepository indexingJobWriteRepository)
    : IRequestHandler<UpdateIndexingJobProgressCommand, ErrorOr<IndexingJob>>
{
    public async Task<ErrorOr<IndexingJob>> Handle(
        UpdateIndexingJobProgressCommand request,
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
            return IndexingJobErrors.ProgressUpdateNotAllowed(indexingJob.Status);
        }

        indexingJob.UpdateIndexingProgress(request.Progress);
        return await indexingJobWriteRepository.UpdateAsync(indexingJob, cancellationToken);
    }
}
