using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record CreateIndexingJobCommand(Guid BookId) : IRequest<ErrorOr<IndexingJob>>;

public sealed class CreateIndexingJobCommandHandler(
    IBookReadRepository bookReadRepository,
    IIndexingJobWriteRepository indexingJobWriteRepository)
    : IRequestHandler<CreateIndexingJobCommand, ErrorOr<IndexingJob>>
{
    public async Task<ErrorOr<IndexingJob>> Handle(
        CreateIndexingJobCommand request,
        CancellationToken cancellationToken)
    {
        var book = await bookReadRepository.GetByIdAsync(request.BookId, cancellationToken);
        if (book is null)
        {
            return BookErrors.BookNotFound(request.BookId);
        }

        var indexingJob = IndexingJob.Create(book.Id);
        return await indexingJobWriteRepository.CreateAsync(indexingJob, cancellationToken);
    }
}
