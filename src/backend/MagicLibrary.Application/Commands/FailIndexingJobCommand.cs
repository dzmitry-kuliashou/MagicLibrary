using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record FailIndexingJobCommand(
    Guid IndexingJobId,
    string FailureMessage,
    DateTimeOffset CompletedAt) : IRequest<ErrorOr<IndexingJob>>;

public sealed class FailIndexingJobCommandHandler(
    IValidator<FailIndexingJobCommand> validator,
    IIndexingJobReadRepository indexingJobReadRepository,
    IIndexingJobWriteRepository indexingJobWriteRepository)
    : IRequestHandler<FailIndexingJobCommand, ErrorOr<IndexingJob>>
{
    public async Task<ErrorOr<IndexingJob>> Handle(
        FailIndexingJobCommand request,
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

        if (!indexingJob.CanTransitionTo(IndexingJobStatus.Failed))
        {
            return IndexingJobErrors.InvalidIndexingTransition(
                indexingJob.Status,
                IndexingJobStatus.Failed);
        }

        indexingJob.Fail(request.FailureMessage, request.CompletedAt);
        return await indexingJobWriteRepository.UpdateAsync(indexingJob, cancellationToken);
    }
}
