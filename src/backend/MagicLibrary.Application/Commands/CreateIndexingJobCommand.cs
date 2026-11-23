using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record CreateIndexingJobCommand(Guid? BookId = null) : IRequest<ErrorOr<IndexingJob>>;

public sealed class CreateIndexingJobCommandHandler(
    IBookReadRepository bookReadRepository,
    IIndexingJobWriteRepository indexingJobWriteRepository)
    : IRequestHandler<CreateIndexingJobCommand, ErrorOr<IndexingJob>>
{
    public async Task<ErrorOr<IndexingJob>> Handle(
        CreateIndexingJobCommand request,
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

        var indexingJob = IndexingJob.Create(request.BookId);
        return await indexingJobWriteRepository.CreateAsync(indexingJob, cancellationToken);
    }
}
