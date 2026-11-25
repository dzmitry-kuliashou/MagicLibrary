using MagicLibrary.Contract.Requests;
using MagicLibrary.Contract.Responses;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Queries;

public sealed record ListBooksQuery(ListParams Parameters) : IRequest<BooksResponse>;

public sealed class ListBooksQueryHandler(IBookReadRepository bookReadRepository)
    : IRequestHandler<ListBooksQuery, BooksResponse>
{
    public async Task<BooksResponse> Handle(
        ListBooksQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Parameters.GetNormalizedPage();
        var pageSize = request.Parameters.GetNormalizedPageSize();

        var (books, totalCount) = await bookReadRepository.ListAsync(
            page,
            pageSize,
            cancellationToken);

        var totalPageCount = totalCount / pageSize
            + (totalCount % pageSize == 0 ? 0 : 1);

        return new BooksResponse
        {
            Items = books
                .Select(book => new BookResponse(
                    book.Id,
                    book.Title,
                    book.Author,
                    book.IndexingStatus.ToString()))
                .ToArray(),
            TotalPageCount = totalPageCount,
            CurrentPage = page,
            PageSize = pageSize
        };
    }
}
