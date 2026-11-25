using ErrorOr;
using MagicLibrary.Api.Controllers;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.Requests;
using MagicLibrary.Contract.Responses;
using MagicLibrary.Core.Aggregates;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Api.Controllers;

public sealed class IndexingJobControllerTests
{
    [Fact]
    public async Task Run_SuccessfulRequest_ReturnsAcceptedResponseWithoutLocation()
    {
        var bookId = Guid.NewGuid();
        var indexingJobId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<RunIndexingCommand>(command => command.BookId == bookId),
                CancellationToken.None))
            .ReturnsAsync((ErrorOr<Guid>)indexingJobId);
        var controller = new IndexingJobController(sender.Object);

        var result = await controller.Run(
            new RunIndexingRequest(bookId),
            CancellationToken.None);

        var response = Assert.IsType<AcceptedResult>(result);
        var responseBody = Assert.IsType<RunIndexingResponse>(response.Value);
        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.Equal(indexingJobId, responseBody.IndexingJobId);
        Assert.Equal(
            new[] { nameof(RunIndexingResponse.IndexingJobId) },
            typeof(RunIndexingResponse).GetProperties().Select(property => property.Name));
        sender.Verify(
            service => service.Send(
                It.Is<RunIndexingCommand>(command => command.BookId == bookId),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task GetById_RunningJob_ReturnsFileErrors()
    {
        var job = IndexingJob.Create(null);
        job.Start(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
        job.AddFileError("first.pdf", "First error.");
        job.AddFileError("second.pdf", "Second error.");
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetIndexingJobByIdQuery>(query => query.IndexingJobId == job.Id),
                CancellationToken.None))
            .ReturnsAsync((ErrorOr<IndexingJob>)job);
        var controller = new IndexingJobController(sender.Object);

        var result = await controller.GetById(job.Id, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        var responseBody = Assert.IsType<IndexingJobResponse>(response.Value);
        Assert.Collection(
            responseBody.FileErrors,
            error =>
            {
                Assert.Equal("first.pdf", error.FileName);
                Assert.Equal("First error.", error.Message);
            },
            error =>
            {
                Assert.Equal("second.pdf", error.FileName);
                Assert.Equal("Second error.", error.Message);
            });
    }
}
