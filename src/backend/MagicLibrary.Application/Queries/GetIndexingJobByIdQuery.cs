using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Queries;

public sealed record GetIndexingJobByIdQuery(Guid IndexingJobId)
    : IRequest<ErrorOr<IndexingJob>>;

public sealed class GetIndexingJobByIdQueryHandler(IIndexingJobReadRepository indexingJobReadRepository)
    : IRequestHandler<GetIndexingJobByIdQuery, ErrorOr<IndexingJob>>
{
    public async Task<ErrorOr<IndexingJob>> Handle(
        GetIndexingJobByIdQuery request,
        CancellationToken cancellationToken)
    {
        var indexingJob = await indexingJobReadRepository.GetByIdAsync(
            request.IndexingJobId,
            cancellationToken);

        return indexingJob is null
            ? IndexingJobErrors.IndexingJobNotFound(request.IndexingJobId)
            : indexingJob;
    }
}
