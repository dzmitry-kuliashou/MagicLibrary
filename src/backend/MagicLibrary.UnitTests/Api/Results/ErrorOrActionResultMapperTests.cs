using ErrorOr;
using MagicLibrary.Api.Results;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MagicLibrary.UnitTests.Api.Results;

public sealed class ErrorOrActionResultMapperTests
{
    [Fact]
    public void ToActionResult_ConflictError_ReturnsConflict()
    {
        var result = (ErrorOr<string>)Error.Conflict(
            "Conversation.AssistantResponsePending",
            "A response is pending.");

        var actionResult = result.ToActionResult(value => new OkObjectResult(value));

        Assert.IsType<ConflictObjectResult>(actionResult);
    }
}
