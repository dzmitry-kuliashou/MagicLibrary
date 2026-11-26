using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class CompleteAssistantResponseCommandValidator : AbstractValidator<CompleteAssistantResponseCommand>
{
    public CompleteAssistantResponseCommandValidator()
    {
        RuleFor(command => command.ConversationId)
            .NotEmpty()
            .WithError(ConversationErrors.InvalidId);

        RuleFor(command => command.Content)
            .Must(content => !string.IsNullOrWhiteSpace(content))
            .WithError(ConversationErrors.MissingContent);

        RuleFor(command => command.SourceReferences)
            .NotNull()
            .WithError(ConversationErrors.MissingSourceReferences);
    }
}
