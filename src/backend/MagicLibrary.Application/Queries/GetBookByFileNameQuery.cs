using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Queries;

public sealed record GetBookByFileNameQuery(string FileName) : IRequest<ErrorOr<Book>>;

public sealed class GetBookByFileNameQueryHandler(IBookReadRepository bookReadRepository)
    : IRequestHandler<GetBookByFileNameQuery, ErrorOr<Book>>
{
    public async Task<ErrorOr<Book>> Handle(
        GetBookByFileNameQuery request,
        CancellationToken cancellationToken)
    {
        var book = await bookReadRepository.FindByFileName(request.FileName, cancellationToken);
        return book is null ? BookErrors.BookNotFound(request.FileName) : book;
    }
}
