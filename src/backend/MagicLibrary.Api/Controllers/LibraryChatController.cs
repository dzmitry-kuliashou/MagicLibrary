using System.Text.Json;
using MagicLibrary.Api.Results;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Conversations;
using MagicLibrary.Contract.Requests;
using Microsoft.AspNetCore.Mvc;

namespace MagicLibrary.Api.Controllers;

[ApiController]
[Route("librarychat")]
public sealed class LibraryChatController(
    ILibraryConversationOrchestrator conversationOrchestrator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] LibraryChatRequest request)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var streamResult = await conversationOrchestrator.StreamAsync(
            new LibraryChatCommand(request.ConversationId, request.Message, request.BookId),
            cancellationToken);
        if (streamResult.IsError)
        {
            return streamResult.ToActionResult(_ => new EmptyResult());
        }

        Response.ContentType = "text/event-stream";
        await foreach (var streamEvent in streamResult.Value)
        {
            var (eventName, data) = streamEvent switch
            {
                ChatStreamEvent.Status status =>
                    ("status", JsonSerializer.Serialize(new { conversationId = status.ConversationId })),
                ChatStreamEvent.Text text =>
                    ("text", JsonSerializer.Serialize(new { delta = text.Delta })),
                ChatStreamEvent.Completed =>
                    ("completed", "{}"),
                ChatStreamEvent.Error error =>
                    ("error", JsonSerializer.Serialize(new { message = error.Message })),
                _ => throw new ArgumentOutOfRangeException(nameof(streamEvent), streamEvent, null)
            };

            await Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        return new EmptyResult();
    }
}
