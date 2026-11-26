using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Core.ValueObjects;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record InterruptAssistantResponseCommand(Guid ConversationId)
    : IRequest<ErrorOr<Conversation>>;

public sealed class InterruptAssistantResponseCommandHandler(
    IValidator<InterruptAssistantResponseCommand> validator,
    IConversationReadRepository conversationReadRepository,
    IConversationWriteRepository conversationWriteRepository,
    TimeProvider timeProvider)
    : IRequestHandler<InterruptAssistantResponseCommand, ErrorOr<Conversation>>
{
    public async Task<ErrorOr<Conversation>> Handle(
        InterruptAssistantResponseCommand request,
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

        if (conversation.Messages.LastOrDefault() is not
            { Role: MessageRole.Assistant, Status: MessageStatus.Pending })
        {
            return ConversationErrors.AssistantResponseNotPending;
        }

        conversation.InterruptAssistantResponse(timeProvider.GetUtcNow());
        return await conversationWriteRepository.UpdateAsync(conversation, cancellationToken);
    }
}
