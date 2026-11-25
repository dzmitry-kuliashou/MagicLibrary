using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithError(ConversationErrors.MissingName);
    }
}
