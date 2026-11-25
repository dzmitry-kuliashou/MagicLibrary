using MagicLibrary.Contract.Requests;
using MagicLibrary.Contract.Responses;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Queries;

public sealed record ListConversationsQuery(ListParams Parameters) : IRequest<ConversationsResponse>;

public sealed class ListConversationsQueryHandler(IConversationReadRepository conversationReadRepository)
    : IRequestHandler<ListConversationsQuery, ConversationsResponse>
{
    public async Task<ConversationsResponse> Handle(
        ListConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Parameters.GetNormalizedPage();
        var pageSize = request.Parameters.GetNormalizedPageSize();

        var (conversations, totalCount) = await conversationReadRepository.ListAsync(
            page,
            pageSize,
            cancellationToken);

        var totalPageCount = totalCount / pageSize
            + (totalCount % pageSize == 0 ? 0 : 1);

        return new ConversationsResponse
        {
            Items = conversations
                .Select(conversation => new ConversationSummaryResponse(
                    conversation.Id,
                    conversation.Name,
                    conversation.CreatedAt,
                    conversation.UpdatedAt))
                .ToArray(),
            TotalPageCount = totalPageCount,
            CurrentPage = page,
            PageSize = pageSize
        };
    }
}
