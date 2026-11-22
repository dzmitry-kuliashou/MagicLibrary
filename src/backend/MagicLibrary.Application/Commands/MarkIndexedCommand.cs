using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record MarkIndexedCommand(
    Guid BookId,
    DateTimeOffset IndexedAt) : IRequest<ErrorOr<Book>>;

public sealed class MarkIndexedCommandHandler(
    IBookReadRepository bookReadRepository,
    IBookWriteRepository bookWriteRepository)
    : IRequestHandler<MarkIndexedCommand, ErrorOr<Book>>
{
    public async Task<ErrorOr<Book>> Handle(
        MarkIndexedCommand request,
        CancellationToken cancellationToken)
    {
        var book = await bookReadRepository.GetByIdAsync(request.BookId, cancellationToken);
        if (book is null)
        {
            return BookErrors.BookNotFound(request.BookId);
        }

        if (!book.CanTransitionTo(BookIndexingStatus.Indexed))
        {
            return BookErrors.InvalidIndexingTransition(
                book.IndexingStatus,
                BookIndexingStatus.Indexed);
        }

        book.MarkIndexed(request.IndexedAt);
        return await bookWriteRepository.UpdateAsync(book, cancellationToken);
    }
}
