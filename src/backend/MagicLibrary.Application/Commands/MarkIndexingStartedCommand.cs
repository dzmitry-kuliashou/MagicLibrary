using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record MarkIndexingStartedCommand(Guid BookId) : IRequest<ErrorOr<Book>>;

public sealed class MarkIndexingStartedCommandHandler(
    IBookReadRepository bookReadRepository,
    IBookWriteRepository bookWriteRepository)
    : IRequestHandler<MarkIndexingStartedCommand, ErrorOr<Book>>
{
    public async Task<ErrorOr<Book>> Handle(
        MarkIndexingStartedCommand request,
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
