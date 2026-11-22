using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;

namespace MagicLibrary.Application.Validators;

public sealed class StartIndexingJobCommandValidator : AbstractValidator<StartIndexingJobCommand>
{
    public StartIndexingJobCommandValidator()
    {
        RuleFor(command => command.StartedAt)
            .Must(startedAt => startedAt != default)
            .WithError(IndexingJobErrors.InvalidStartTimestamp);
    }
}
