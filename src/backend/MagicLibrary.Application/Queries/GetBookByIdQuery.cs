using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Queries;

public sealed record GetBookByIdQuery(Guid Id) : IRequest<ErrorOr<Book>>;

public sealed class GetBookByIdQueryHandler(IBookReadRepository bookReadRepository)
    : IRequestHandler<GetBookByIdQuery, ErrorOr<Book>>
{
    public async Task<ErrorOr<Book>> Handle(
        GetBookByIdQuery request,
        CancellationToken cancellationToken)
    {
        var book = await bookReadRepository.GetByIdAsync(request.Id, cancellationToken);

        return book is null ? BookErrors.BookNotFound(request.Id) : book;
    }
}
