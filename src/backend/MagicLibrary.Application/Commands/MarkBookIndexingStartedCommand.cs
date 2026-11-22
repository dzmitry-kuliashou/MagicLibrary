using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record MarkBookIndexingStartedCommand(Guid BookId) : IRequest<ErrorOr<Book>>;

public sealed class MarkBookIndexingStartedCommandHandler(
    IBookReadRepository bookReadRepository,
    IBookWriteRepository bookWriteRepository)
    : IRequestHandler<MarkBookIndexingStartedCommand, ErrorOr<Book>>
{
    public async Task<ErrorOr<Book>> Handle(
        MarkBookIndexingStartedCommand request,
        CancellationToken cancellationToken)
    {
        var book = await bookReadRepository.GetByIdAsync(request.BookId, cancellationToken);
        if (book is null)
        {
            return BookErrors.BookNotFound(request.BookId);
        }

        if (!book.CanTransitionTo(BookIndexingStatus.InProgress))
        {
            return BookErrors.InvalidIndexingTransition(
                book.IndexingStatus,
                BookIndexingStatus.InProgress);
        }

        book.MarkIndexingStarted();
        return await bookWriteRepository.UpdateAsync(book, cancellationToken);
    }
}
