using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Core.ValueObjects;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record BeginAssistantResponseCommand(Guid ConversationId)
    : IRequest<ErrorOr<Conversation>>;

public sealed class BeginAssistantResponseCommandHandler(
    IValidator<BeginAssistantResponseCommand> validator,
    IConversationReadRepository conversationReadRepository,
    IConversationWriteRepository conversationWriteRepository,
    TimeProvider timeProvider)
    : IRequestHandler<BeginAssistantResponseCommand, ErrorOr<Conversation>>
{
    public async Task<ErrorOr<Conversation>> Handle(
        BeginAssistantResponseCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return validationResult.Errors.ToErrors();
        }

        var conversation = await conversationReadRepository.GetByIdAsync(
            request.ConversationId,
            cancellationToken);
        if (conversation is null)
        {
            return ConversationErrors.ConversationNotFound(request.ConversationId);
        }

        if (conversation.Messages.LastOrDefault()?.Status == MessageStatus.Pending)
        {
            return ConversationErrors.AssistantResponsePending;
        }

        conversation.BeginAssistantResponse(timeProvider.GetUtcNow());
        return await conversationWriteRepository.UpdateAsync(conversation, cancellationToken);
    }
}
