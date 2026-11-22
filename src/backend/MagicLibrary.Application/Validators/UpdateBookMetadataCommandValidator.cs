using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class UpdateBookMetadataCommandValidator : AbstractValidator<UpdateBookMetadataCommand>
{
    public UpdateBookMetadataCommandValidator()
    {
        RuleFor(command => command.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithError(BookErrors.MissingTitle);
    }
}
