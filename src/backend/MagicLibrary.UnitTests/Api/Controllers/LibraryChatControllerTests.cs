using System.Text;
using ErrorOr;
using MagicLibrary.Api.Controllers;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Conversations;
using MagicLibrary.Application.Errors;
using MagicLibrary.Contract.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Api.Controllers;

public sealed class LibraryChatControllerTests
{
    [Fact]
    public async Task Post_ValidRequest_StreamsEventsAndConversationId()
    {
        const string message = "Question";
        var conversationId = Guid.NewGuid();
        using var cancellationSource = new CancellationTokenSource();
        Controller.HttpContext.RequestAborted = cancellationSource.Token;
        Orchestrator
            .Setup(service => service.StreamAsync(
                It.Is<LibraryChatCommand>(command =>
                    command.ConversationId == conversationId && command.Message == message),
                cancellationSource.Token))
            .ReturnsAsync(ErrorOrFactory.From(GetEvents()));

        var result = await Controller.Post(new LibraryChatRequest(conversationId, message));

        Assert.IsType<EmptyResult>(result);
        Assert.Equal("text/event-stream", Controller.Response.ContentType);
        Assert.Equal(
            $"event: status\ndata: {{\"conversationId\":\"{conversationId}\"}}\n\n" +
            "event: text\ndata: {\"delta\":\"Answer\"}\n\n" +
            "event: completed\ndata: {}\n\n",
            Encoding.UTF8.GetString(ResponseBody.ToArray()));
        Orchestrator.Verify(
            service => service.StreamAsync(
                It.Is<LibraryChatCommand>(command =>
                    command.ConversationId == conversationId && command.Message == message),
                cancellationSource.Token),
            Times.Once);

        async IAsyncEnumerable<ChatStreamEvent> GetEvents()
        {
            yield return new ChatStreamEvent.Status(conversationId);
            await Task.Yield();
            yield return new ChatStreamEvent.Text("Answer");
            await Task.Yield();
            yield return new ChatStreamEvent.Completed();
        }
    }

    [Fact]
    public async Task Post_UnknownConversation_ReturnsNotFoundBeforeSse()
    {
        var conversationId = Guid.NewGuid();
        const string message = "Question";
        Orchestrator
            .Setup(service => service.StreamAsync(
                It.Is<LibraryChatCommand>(command =>
                    command.ConversationId == conversationId && command.Message == message),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (ErrorOr<IAsyncEnumerable<ChatStreamEvent>>)
                ConversationErrors.ConversationNotFound(conversationId));

        var result = await Controller.Post(new LibraryChatRequest(conversationId, message));

        var response = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Null(Controller.Response.ContentType);
        Assert.Empty(ResponseBody.ToArray());
        Orchestrator.Verify(
            service => service.StreamAsync(
                It.Is<LibraryChatCommand>(command =>
                    command.ConversationId == conversationId && command.Message == message),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null, " ")]
    [InlineData("00000000-0000-0000-0000-000000000000", "Question")]
    public async Task Post_InvalidRequest_ReturnsBadRequestBeforeSse(
        string? conversationIdText,
        string message)
    {
        Guid? conversationId = conversationIdText is null
            ? null
            : Guid.Parse(conversationIdText);
        var error = conversationId == Guid.Empty
            ? ConversationErrors.InvalidId
            : ConversationErrors.MissingContent;
        Orchestrator
            .Setup(service => service.StreamAsync(
                It.Is<LibraryChatCommand>(command =>
                    command.ConversationId == conversationId && command.Message == message),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ErrorOr<IAsyncEnumerable<ChatStreamEvent>>)error);

        var result = await Controller.Post(new LibraryChatRequest(conversationId, message));

        var response = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Null(Controller.Response.ContentType);
        Assert.Empty(ResponseBody.ToArray());
        Orchestrator.Verify(
            service => service.StreamAsync(
                It.Is<LibraryChatCommand>(command =>
                    command.ConversationId == conversationId && command.Message == message),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    public LibraryChatControllerTests()
    {
        Controller = new LibraryChatController(Orchestrator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        Controller.HttpContext.Response.Body = ResponseBody;
    }

    public Mock<ILibraryConversationOrchestrator> Orchestrator { get; } = new();

    public MemoryStream ResponseBody { get; } = new();

    public LibraryChatController Controller { get; }
}
