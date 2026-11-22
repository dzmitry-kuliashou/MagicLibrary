using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class CompleteIndexingJobCommandValidator : AbstractValidator<CompleteIndexingJobCommand>
{
    public CompleteIndexingJobCommandValidator()
    {
        RuleFor(command => command.CompletedAt)
            .Must(completedAt => completedAt != default)
            .WithError(IndexingJobErrors.InvalidCompletionTimestamp);
    }
}
