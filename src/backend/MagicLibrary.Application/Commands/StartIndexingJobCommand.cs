using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record StartIndexingJobCommand(Guid IndexingJobId, DateTimeOffset StartedAt)
    : IRequest<ErrorOr<IndexingJob>>;

public sealed class StartIndexingJobCommandHandler(
    IValidator<StartIndexingJobCommand> validator,
    IIndexingJobReadRepository indexingJobReadRepository,
    IIndexingJobWriteRepository indexingJobWriteRepository)
    : IRequestHandler<StartIndexingJobCommand, ErrorOr<IndexingJob>>
{
    public async Task<ErrorOr<IndexingJob>> Handle(
        StartIndexingJobCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return validationResult.Errors.ToErrors();
        }

        var indexingJob = await indexingJobReadRepository.GetByIdAsync(
            request.IndexingJobId,
            cancellationToken);
        if (indexingJob is null)
        {
            return IndexingJobErrors.IndexingJobNotFound(request.IndexingJobId);
        }

        if (!indexingJob.CanTransitionTo(IndexingJobStatus.Running))
        {
            return IndexingJobErrors.InvalidIndexingTransition(
                indexingJob.Status,
                IndexingJobStatus.Running);
        }

        indexingJob.Start(request.StartedAt);
        return await indexingJobWriteRepository.UpdateAsync(indexingJob, cancellationToken);
    }
}
