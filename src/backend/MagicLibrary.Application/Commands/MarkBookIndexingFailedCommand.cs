using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record MarkBookIndexingFailedCommand(Guid BookId) : IRequest<ErrorOr<Book>>;

public sealed class MarkBookIndexingFailedCommandHandler(
    IBookReadRepository bookReadRepository,
    IBookWriteRepository bookWriteRepository)
    : IRequestHandler<MarkBookIndexingFailedCommand, ErrorOr<Book>>
{
    public async Task<ErrorOr<Book>> Handle(
        MarkBookIndexingFailedCommand request,
        CancellationToken cancellationToken)
    {
        var book = await bookReadRepository.GetByIdAsync(request.BookId, cancellationToken);
        if (book is null)
        {
            return BookErrors.BookNotFound(request.BookId);
        }

        if (!book.CanTransitionTo(BookIndexingStatus.Failed))
        {
            return BookErrors.InvalidIndexingTransition(
                book.IndexingStatus,
                BookIndexingStatus.Failed);
        }

        book.MarkIndexingFailed();
        return await bookWriteRepository.UpdateAsync(book, cancellationToken);
    }
}
