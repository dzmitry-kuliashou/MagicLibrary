using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class LibraryChatCommandValidator : AbstractValidator<LibraryChatCommand>
{
    public LibraryChatCommandValidator()
    {
        RuleFor(command => command.ConversationId)
            .Must(conversationId => conversationId is null || conversationId != Guid.Empty)
            .WithError(ConversationErrors.InvalidId);

        RuleFor(command => command.Message)
            .Must(message => !string.IsNullOrWhiteSpace(message))
            .WithError(ConversationErrors.MissingContent);
    }
}
