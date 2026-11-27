using MagicLibrary.Contract.Responses;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Queries;

public sealed record ListBookConversationsQuery(Guid BookId)
    : IRequest<IReadOnlyList<ConversationSummaryResponse>>;

public sealed class ListBookConversationsQueryHandler(
    IConversationReadRepository conversationReadRepository)
    : IRequestHandler<ListBookConversationsQuery, IReadOnlyList<ConversationSummaryResponse>>
{
    public async Task<IReadOnlyList<ConversationSummaryResponse>> Handle(
        ListBookConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var conversations = await conversationReadRepository.ListBookConversationsAsync(
            request.BookId,
            cancellationToken);

        return conversations
            .Select(conversation => new ConversationSummaryResponse(
                conversation.Id,
                conversation.Name,
                conversation.CreatedAt,
                conversation.UpdatedAt))
            .ToArray();
    }
}
