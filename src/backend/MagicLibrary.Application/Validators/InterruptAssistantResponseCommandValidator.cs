using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class InterruptAssistantResponseCommandValidator : AbstractValidator<InterruptAssistantResponseCommand>
{
    public InterruptAssistantResponseCommandValidator()
    {
        RuleFor(command => command.ConversationId)
            .NotEmpty()
            .WithError(ConversationErrors.InvalidId);
    }
}
