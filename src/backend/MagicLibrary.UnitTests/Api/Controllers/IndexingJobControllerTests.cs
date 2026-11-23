using ErrorOr;
using MagicLibrary.Api.Controllers;
using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.IndexingJobs.Responses;
using MagicLibrary.Core.Aggregates;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Api.Controllers;

public sealed class IndexingJobControllerTests
{
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
