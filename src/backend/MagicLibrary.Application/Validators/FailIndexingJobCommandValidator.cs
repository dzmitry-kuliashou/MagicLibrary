using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class FailIndexingJobCommandValidator : AbstractValidator<FailIndexingJobCommand>
{
    public FailIndexingJobCommandValidator()
    {
        RuleFor(command => command.FailureMessage)
            .Must(failureMessage => !string.IsNullOrWhiteSpace(failureMessage))
            .WithError(IndexingJobErrors.MissingFailureMessage);

        RuleFor(command => command.CompletedAt)
            .Must(completedAt => completedAt != default)
            .WithError(IndexingJobErrors.InvalidCompletionTimestamp);
    }
}
