using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Core.ValueObjects;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record AddAssistantCommand(
    Guid ConversationId,
    string Content,
    IEnumerable<SourceReference> SourceReferences)
    : IRequest<ErrorOr<Conversation>>;

public sealed class AddAssistantCommandHandler(
    IValidator<AddAssistantCommand> validator,
    IConversationReadRepository conversationReadRepository,
    IConversationWriteRepository conversationWriteRepository,
    TimeProvider timeProvider)
    : IRequestHandler<AddAssistantCommand, ErrorOr<Conversation>>
{
    public async Task<ErrorOr<Conversation>> Handle(
        AddAssistantCommand request,
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

        conversation.AddAssistantMessage(
            request.Content,
            request.SourceReferences,
            timeProvider.GetUtcNow());
        return await conversationWriteRepository.UpdateAsync(conversation, cancellationToken);
    }
}
