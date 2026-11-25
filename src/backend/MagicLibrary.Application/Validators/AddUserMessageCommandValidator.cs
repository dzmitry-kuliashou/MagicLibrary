using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class AddUserMessageCommandValidator : AbstractValidator<AddUserMessageCommand>
{
    public AddUserMessageCommandValidator()
    {
        RuleFor(command => command.ConversationId)
            .NotEmpty()
            .WithError(ConversationErrors.InvalidId);

        RuleFor(command => command.Content)
            .Must(content => !string.IsNullOrWhiteSpace(content))
            .WithError(ConversationErrors.MissingContent);
    }
}
