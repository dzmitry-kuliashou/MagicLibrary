using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record CompleteIndexingJobCommand(Guid IndexingJobId, DateTimeOffset CompletedAt)
    : IRequest<ErrorOr<IndexingJob>>;

public sealed class CompleteIndexingJobCommandHandler(
    IValidator<CompleteIndexingJobCommand> validator,
    IIndexingJobReadRepository indexingJobReadRepository,
    IIndexingJobWriteRepository indexingJobWriteRepository)
    : IRequestHandler<CompleteIndexingJobCommand, ErrorOr<IndexingJob>>
{
    public async Task<ErrorOr<IndexingJob>> Handle(
        CompleteIndexingJobCommand request,
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

        if (!indexingJob.CanTransitionTo(IndexingJobStatus.Completed))
        {
            return IndexingJobErrors.InvalidIndexingTransition(
                indexingJob.Status,
                IndexingJobStatus.Completed);
        }

        indexingJob.Complete(request.CompletedAt);
        return await indexingJobWriteRepository.UpdateAsync(indexingJob, cancellationToken);
    }
}
