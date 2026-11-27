using System.Text.RegularExpressions;
using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Core.ValueObjects;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record CompleteAssistantResponseCommand(
    Guid ConversationId,
    string Content,
    IEnumerable<SourceReference> SourceReferences)
    : IRequest<ErrorOr<Conversation>>;

public sealed class CompleteAssistantResponseCommandHandler(
    IValidator<CompleteAssistantResponseCommand> validator,
    IConversationReadRepository conversationReadRepository,
    IConversationWriteRepository conversationWriteRepository,
    TimeProvider timeProvider)
    : IRequestHandler<CompleteAssistantResponseCommand, ErrorOr<Conversation>>
{
    private static readonly Regex ThinkingContentRegex = new(
        @"<think\b[^>]*>.*?(?:</think\s*>|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public async Task<ErrorOr<Conversation>> Handle(
        CompleteAssistantResponseCommand request,
        CancellationToken cancellationToken)
    {
        var content = ClearThinkingContent(request.Content);
        var validationResult = await validator.ValidateAsync(
            request with { Content = content },
            cancellationToken);
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

        conversation.CompleteAssistantResponse(
            content,
            request.SourceReferences,
            timeProvider.GetUtcNow());
        return await conversationWriteRepository.UpdateAsync(conversation, cancellationToken);
    }

    private static string ClearThinkingContent(string? content) =>
        content is null ? string.Empty : ThinkingContentRegex.Replace(content, string.Empty).Trim();
}
