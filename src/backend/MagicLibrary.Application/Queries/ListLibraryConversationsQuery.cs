using MagicLibrary.Contract.Responses;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Queries;

public sealed record ListLibraryConversationsQuery : IRequest<IReadOnlyList<ConversationSummaryResponse>>;

public sealed class ListLibraryConversationsQueryHandler(
    IConversationReadRepository conversationReadRepository)
    : IRequestHandler<ListLibraryConversationsQuery, IReadOnlyList<ConversationSummaryResponse>>
{
    public async Task<IReadOnlyList<ConversationSummaryResponse>> Handle(
        ListLibraryConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var conversations = await conversationReadRepository.ListLibraryConversationsAsync(cancellationToken);

        return conversations
            .Select(conversation => new ConversationSummaryResponse(
                conversation.Id,
                conversation.Name,
                conversation.CreatedAt,
                conversation.UpdatedAt))
            .ToArray();
    }
}
