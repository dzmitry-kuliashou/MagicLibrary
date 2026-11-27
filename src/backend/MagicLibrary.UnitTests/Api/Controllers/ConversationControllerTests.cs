using ErrorOr;
using MagicLibrary.Api.Controllers;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.Requests;
using MagicLibrary.Contract.Responses;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Api.Controllers;

public sealed class ConversationControllerTests
{
    [Fact]
    public async Task GetById_MissingConversation_ReturnsNotFound()
    {
        var conversationId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetConversationByIdQuery>(query => query.Id == conversationId),
                CancellationToken.None))
            .ReturnsAsync((ErrorOr<Conversation>)ConversationErrors.ConversationNotFound(conversationId));
        var controller = new ConversationController(sender.Object);

        var result = await controller.GetById(conversationId, CancellationToken.None);

        var response = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, response.StatusCode);
        sender.Verify(
            service => service.Send(
                It.Is<GetConversationByIdQuery>(query => query.Id == conversationId),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task GetById_ExistingConversation_ReturnsConversationResponse()
    {
        const string systemMessage = "System instructions";
        const string userContent = "Question";
        const string assistantContent = "Answer";
        const string bookTitle = "Library Guide";
        const int pageNumber = 4;
        var bookId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(3);
        var conversation = Conversation.Create("Research", createdAt, systemMessage, bookId);
        conversation.AddUserMessage(userContent, createdAt.AddMinutes(1));
        conversation.BeginAssistantResponse(createdAt.AddMinutes(2));
        conversation.CompleteAssistantResponse(
            assistantContent,
            [SourceReference.Create(bookId, bookTitle, pageNumber, chunkId)],
            updatedAt);
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetConversationByIdQuery>(query => query.Id == conversation.Id),
                CancellationToken.None))
            .ReturnsAsync((ErrorOr<Conversation>)conversation);
        var controller = new ConversationController(sender.Object);

        var result = await controller.GetById(conversation.Id, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        var responseBody = Assert.IsType<ConversationResponse>(response.Value);
        Assert.Equal(conversation.Id, responseBody.Id);
        Assert.Equal(conversation.Name, responseBody.Name);
        Assert.Equal(bookId, responseBody.BookId);
        Assert.Equal(createdAt, responseBody.CreatedAt);
        Assert.Equal(updatedAt, responseBody.UpdatedAt);
        Assert.False(responseBody.IsLastMessagePending);
        Assert.Collection(
            responseBody.Messages,
            message =>
            {
                Assert.Equal("System", message.Role);
                Assert.Equal("Completed", message.Status);
                Assert.Equal(systemMessage, message.Content);
                Assert.Empty(message.SourceReferences);
            },
            message =>
            {
                Assert.Equal("User", message.Role);
                Assert.Equal("Completed", message.Status);
                Assert.Equal(userContent, message.Content);
                Assert.Empty(message.SourceReferences);
            },
            message =>
            {
                Assert.Equal("Assistant", message.Role);
                Assert.Equal("Completed", message.Status);
                Assert.Equal(assistantContent, message.Content);
                var sourceReference = Assert.Single(message.SourceReferences);
                Assert.Equal(bookId, sourceReference.BookId);
                Assert.Equal(bookTitle, sourceReference.BookTitle);
                Assert.Equal(pageNumber, sourceReference.PageNumber);
                Assert.Equal(chunkId, sourceReference.ChunkId);
            });
        sender.Verify(
            service => service.Send(
                It.Is<GetConversationByIdQuery>(query => query.Id == conversation.Id),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task GetById_LastMessagePending_MarksOnlyLastMessageAsPending()
    {
        var createdAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var conversation = Conversation.Create("Research", createdAt, "System instructions");
        conversation.AddUserMessage("Question", createdAt.AddMinutes(1));
        conversation.BeginAssistantResponse(createdAt.AddMinutes(2));
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetConversationByIdQuery>(query => query.Id == conversation.Id),
                CancellationToken.None))
            .ReturnsAsync((ErrorOr<Conversation>)conversation);
        var controller = new ConversationController(sender.Object);

        var result = await controller.GetById(conversation.Id, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        var responseBody = Assert.IsType<ConversationResponse>(response.Value);
        Assert.True(responseBody.IsLastMessagePending);
        Assert.Collection(
            responseBody.Messages,
            message =>
            {
                Assert.Equal("Completed", message.Status);
            },
            message =>
            {
                Assert.Equal("Completed", message.Status);
            },
            message =>
            {
                Assert.Equal("Pending", message.Status);
            });
    }

    [Fact]
    public async Task List_ReturnsPaginatedResponseAndSendsQuery()
    {
        var parameters = new ListParams { Page = "2", PageSize = "3" };
        var expectedResponse = new ConversationsResponse
        {
            Items = Array.Empty<ConversationSummaryResponse>(),
            TotalPageCount = 4,
            CurrentPage = 2,
            PageSize = 3
        };
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<ListConversationsQuery>(query => ReferenceEquals(query.Parameters, parameters)),
                CancellationToken.None))
            .ReturnsAsync(expectedResponse);
        var controller = new ConversationController(sender.Object);

        var result = await controller.List(parameters, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expectedResponse, response.Value);
        sender.Verify(
            service => service.Send(
                It.Is<ListConversationsQuery>(query => ReferenceEquals(query.Parameters, parameters)),
                CancellationToken.None),
            Times.Once);
    }
}
