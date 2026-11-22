using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class CreateBookCommandValidator : AbstractValidator<CreateBookCommand>
{
    public CreateBookCommandValidator()
    {
        RuleFor(command => command.FileName)
            .Cascade(CascadeMode.Stop)
            .Must(fileName => !string.IsNullOrWhiteSpace(fileName))
            .WithError(BookErrors.MissingFileName)
            .Must(IsLeafFileName)
            .WithError(BookErrors.UnsafeFileName)
            .Must(IsPdfFileName)
            .WithError(BookErrors.InvalidExtension);

        RuleFor(command => command.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithError(BookErrors.MissingTitle);
    }

    private static bool IsLeafFileName(string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && !Path.IsPathRooted(fileName)
            && !fileName.Contains('/')
            && !fileName.Contains('\\')
            && fileName is not "." and not "..";
    }

    private static bool IsPdfFileName(string? fileName)
    {
        return string.Equals(
            Path.GetExtension(fileName),
            ".pdf",
            StringComparison.OrdinalIgnoreCase);
    }
}
