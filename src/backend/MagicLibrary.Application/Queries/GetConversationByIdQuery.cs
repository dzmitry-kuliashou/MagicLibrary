using ErrorOr;
using MagicLibrary.Application.Errors;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Queries;

public sealed record GetConversationByIdQuery(Guid Id) : IRequest<ErrorOr<Conversation>>;

public sealed class GetConversationByIdQueryHandler(IConversationReadRepository conversationReadRepository)
    : IRequestHandler<GetConversationByIdQuery, ErrorOr<Conversation>>
{
    public async Task<ErrorOr<Conversation>> Handle(
        GetConversationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var conversation = await conversationReadRepository.GetByIdAsync(
            request.Id,
            cancellationToken);

        return conversation is null
            ? ConversationErrors.ConversationNotFound(request.Id)
            : conversation;
    }
}
