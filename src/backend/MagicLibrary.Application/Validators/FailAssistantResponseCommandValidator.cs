using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class FailAssistantResponseCommandValidator : AbstractValidator<FailAssistantResponseCommand>
{
    public FailAssistantResponseCommandValidator()
    {
        RuleFor(command => command.ConversationId)
            .NotEmpty()
            .WithError(ConversationErrors.InvalidId);
    }
}
