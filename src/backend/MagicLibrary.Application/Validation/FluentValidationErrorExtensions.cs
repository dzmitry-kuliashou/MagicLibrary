using ErrorOr;
using FluentValidation;
using FluentValidation.Results;

namespace MagicLibrary.Application.Validation;

public static class FluentValidationErrorExtensions
{
    public static IRuleBuilderOptions<T, TProperty> WithError<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule,
        Error error) =>
        rule.WithMessage(error.Description)
            .WithErrorCode(error.Code);

    public static List<Error> ToErrors(this IEnumerable<ValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        return failures
            .Select(failure => Error.Validation(
                failure.ErrorCode,
                failure.ErrorMessage,
                new Dictionary<string, object>
                {
                    ["PropertyName"] = failure.PropertyName
                }))
            .ToList();
    }
}
