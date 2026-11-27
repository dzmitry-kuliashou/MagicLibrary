using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record ResetBookIndexingCommand(Guid BookId) : IRequest<ErrorOr<Book>>;

public sealed class ResetBookIndexingCommandHandler(
    IBookReadRepository bookReadRepository,
    IBookWriteRepository bookWriteRepository)
    : IRequestHandler<ResetBookIndexingCommand, ErrorOr<Book>>
{
    public async Task<ErrorOr<Book>> Handle(
        ResetBookIndexingCommand request,
        CancellationToken cancellationToken)
    {
        var book = await bookReadRepository.GetByIdAsync(request.BookId, cancellationToken);
        if (book is null)
        {
            return BookErrors.BookNotFound(request.BookId);
        }

        book.ResetIndexing();
        return await bookWriteRepository.UpdateAsync(book, cancellationToken);
    }
}
