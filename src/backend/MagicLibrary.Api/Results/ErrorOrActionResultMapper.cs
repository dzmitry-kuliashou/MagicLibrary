using ErrorOr;
using Microsoft.AspNetCore.Mvc;

namespace MagicLibrary.Api.Results;

public static class ErrorOrActionResultMapper
{
    public static IActionResult ToActionResult<T>(
        this ErrorOr<T> result,
        Func<T, IActionResult> onSuccess)
    {
        if (!result.IsError)
        {
            return onSuccess(result.Value);
        }

        var errors = result.Errors;
        if (errors.All(error => error.Type == ErrorType.Validation))
        {
            return new BadRequestObjectResult(errors);
        }

        if (errors.All(error => error.Type == ErrorType.NotFound))
        {
            return new NotFoundObjectResult(errors);
        }

        if (errors.All(error => error.Type == ErrorType.Conflict))
        {
            return new ConflictObjectResult(errors);
        }

        return new ObjectResult(errors)
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
    }
}
