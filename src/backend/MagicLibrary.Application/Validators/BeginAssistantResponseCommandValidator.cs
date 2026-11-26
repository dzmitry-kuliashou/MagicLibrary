using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class BeginAssistantResponseCommandValidator : AbstractValidator<BeginAssistantResponseCommand>
{
    public BeginAssistantResponseCommandValidator()
    {
        RuleFor(command => command.ConversationId)
            .NotEmpty()
            .WithError(ConversationErrors.InvalidId);
    }
}
