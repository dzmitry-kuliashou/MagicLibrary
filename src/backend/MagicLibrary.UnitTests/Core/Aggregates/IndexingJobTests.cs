using MagicLibrary.Core.Aggregates;
using Xunit;

namespace MagicLibrary.UnitTests.Core.Aggregates;

public sealed class IndexingJobTests
{
    [Fact]
    public void Create_BookId_CreatesPendingAttempt()
    {
        var bookId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var job = IndexingJob.Create(bookId);

        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.NotEqual(bookId, job.Id);
        Assert.Equal(bookId, job.BookId);
        Assert.Equal(IndexingJobStatus.Pending, job.Status);
        Assert.Null(job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.FailureMessage);
    }

    [Fact]
    public void Create_DefaultBookId_RejectsValue()
    {
        Assert.Throws<ArgumentException>(() => IndexingJob.Create(Guid.Empty));
    }

    [Fact]
    public void Start_PendingAttempt_RecordsStartTime()
    {
        var job = IndexingJob.Create(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

        job.Start(startedAt);

        Assert.Equal(startedAt, job.StartedAt);
        Assert.Equal(IndexingJobStatus.Running, job.Status);
    }

    [Fact]
    public void Start_DefaultTimestamp_RejectsValueWithoutChangingAttempt()
    {
        var job = CreateJob();

        Assert.Throws<ArgumentException>(() => job.Start(default));

        Assert.Equal(IndexingJobStatus.Pending, job.Status);
        Assert.Null(job.StartedAt);
    }

    [Fact]
    public void Complete_RunningAttempt_RecordsCompletionTime()
    {
        var job = CreateJob();
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var completedAt = startedAt.AddMinutes(2);
        job.Start(startedAt);

        job.Complete(completedAt);

        Assert.Equal(IndexingJobStatus.Completed, job.Status);
        Assert.Equal(startedAt, job.StartedAt);
        Assert.Equal(completedAt, job.CompletedAt);
        Assert.Null(job.FailureMessage);
    }

    [Fact]
    public void Complete_DefaultTimestamp_RejectsValueWithoutChangingAttempt()
    {
        var job = CreateJob();
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        job.Start(startedAt);

        Assert.Throws<ArgumentException>(() => job.Complete(default));

        Assert.Equal(IndexingJobStatus.Running, job.Status);
        Assert.Equal(startedAt, job.StartedAt);
        Assert.Null(job.CompletedAt);
    }

    [Fact]
    public void Fail_RunningAttempt_RecordsErrorAndCompletion()
    {
        var job = CreateJob();
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var completedAt = startedAt.AddMinutes(2);
        job.Start(startedAt);

        job.Fail("Indexing failed.", completedAt);

        Assert.Equal(IndexingJobStatus.Failed, job.Status);
        Assert.Equal(startedAt, job.StartedAt);
        Assert.Equal(completedAt, job.CompletedAt);
        Assert.Equal("Indexing failed.", job.FailureMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Fail_BlankFailureMessage_RejectsValueWithoutChangingAttempt(string? failureMessage)
    {
        var job = CreateJob();
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        job.Start(startedAt);

        Assert.ThrowsAny<ArgumentException>(() => job.Fail(
            failureMessage!,
            startedAt.AddMinutes(2)));

        Assert.Equal(IndexingJobStatus.Running, job.Status);
        Assert.Equal(startedAt, job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.FailureMessage);
    }

    [Fact]
    public void Fail_DefaultCompletionTimestamp_RejectsValueWithoutChangingAttempt()
    {
        var job = CreateJob();
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        job.Start(startedAt);

        Assert.Throws<ArgumentException>(() => job.Fail("Indexing failed.", default));

        Assert.Equal(IndexingJobStatus.Running, job.Status);
        Assert.Equal(startedAt, job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.FailureMessage);
    }

    private static IndexingJob CreateJob()
    {
        return IndexingJob.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }
}
