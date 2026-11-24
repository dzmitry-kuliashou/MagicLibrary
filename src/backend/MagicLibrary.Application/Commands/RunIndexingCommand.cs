using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Indexing;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record RunIndexingCommand(Guid? BookId) : IRequest<ErrorOr<Guid>>;

public sealed class RunIndexingJobCommandHandler(
    IBookReadRepository bookReadRepository,
    IIndexingJobWriteRepository indexingJobWriteRepository,
    IIndexingJobBackgroundProcessor backgroundProcessor)
    : IRequestHandler<RunIndexingCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(
        RunIndexingCommand request,
        CancellationToken cancellationToken)
    {
        if (request.BookId is Guid bookId)
        {
            var book = await bookReadRepository.GetByIdAsync(bookId, cancellationToken);
            if (book is null)
            {
                return BookErrors.BookNotFound(bookId);
            }
        }

        var createdJob = await indexingJobWriteRepository.CreateAsync(
            IndexingJob.Create(request.BookId),
            cancellationToken);

        var indexingJobId = createdJob.Id;
        // This simple MVP approach is not durable; a message broker would enable retries and stronger reliability policies.
        backgroundProcessor.Start(indexingJobId);
        return indexingJobId;
    }
}
