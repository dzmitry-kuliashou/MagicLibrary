using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record AddBookKeywordsCommand(Guid BookId, IEnumerable<string> Keywords)
    : IRequest<ErrorOr<Book>>;

public sealed class AddBookKeywordsCommandHandler(
    IBookReadRepository bookReadRepository,
    IBookWriteRepository bookWriteRepository)
    : IRequestHandler<AddBookKeywordsCommand, ErrorOr<Book>>
{
    public async Task<ErrorOr<Book>> Handle(
        AddBookKeywordsCommand request,
        CancellationToken cancellationToken)
    {
        var book = await bookReadRepository.GetByIdAsync(request.BookId, cancellationToken);
        if (book is null)
        {
            return BookErrors.BookNotFound(request.BookId);
        }

        book.AddKeywords(request.Keywords);
        return await bookWriteRepository.UpdateAsync(book, cancellationToken);
    }
}
