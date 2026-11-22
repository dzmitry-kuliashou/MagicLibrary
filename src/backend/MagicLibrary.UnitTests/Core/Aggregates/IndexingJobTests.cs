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
        Assert.Equal(string.Empty, job.Progress);
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
    public void Reconstitute_FailedAttempt_RestoresAllFields()
    {
        var id = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(-5));
        var completedAt = startedAt.AddMinutes(2);

        var job = IndexingJob.Reconstitute(
            id,
            bookId,
            IndexingJobStatus.Failed,
            startedAt,
            completedAt,
            "Indexing failed.");

        Assert.Equal(id, job.Id);
        Assert.Equal(bookId, job.BookId);
        Assert.Equal(IndexingJobStatus.Failed, job.Status);
        Assert.Equal(startedAt, job.StartedAt);
        Assert.Equal(startedAt.Offset, job.StartedAt!.Value.Offset);
        Assert.Equal(completedAt, job.CompletedAt);
        Assert.Equal(completedAt.Offset, job.CompletedAt!.Value.Offset);
        Assert.Equal("Indexing failed.", job.FailureMessage);
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

    [Theory]
    [InlineData(IndexingJobStatus.Running)]
    [InlineData(IndexingJobStatus.Completed)]
    [InlineData(IndexingJobStatus.Failed)]
    public void Start_NonPendingAttempt_RejectsTransitionWithoutChangingAttempt(IndexingJobStatus status)
    {
        var job = CreateJob();
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var completedAt = startedAt.AddMinutes(2);
        job.Start(startedAt);

        if (status == IndexingJobStatus.Completed)
        {
            job.Complete(completedAt);
        }
        else if (status == IndexingJobStatus.Failed)
        {
            job.Fail("Indexing failed.", completedAt);
        }

        Assert.Throws<ArgumentException>(() => job.Start(completedAt.AddMinutes(1)));

        Assert.Equal(status, job.Status);
        Assert.Equal(startedAt, job.StartedAt);
        Assert.Equal(status == IndexingJobStatus.Running ? null : completedAt, job.CompletedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Indexing page 1 of 3")]
    public void UpdateIndexingProgress_RunningAttempt_StoresExactProgress(string progress)
    {
        var job = CreateJob();
        job.Start(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));

        job.UpdateIndexingProgress(progress);

        Assert.Equal(IndexingJobStatus.Running, job.Status);
        Assert.Equal(progress, job.Progress);
    }

    [Theory]
    [InlineData(IndexingJobStatus.Pending)]
    [InlineData(IndexingJobStatus.Completed)]
    [InlineData(IndexingJobStatus.Failed)]
    public void UpdateIndexingProgress_NonRunningAttempt_RejectsValueWithoutChangingAttempt(
        IndexingJobStatus status)
    {
        var job = IndexingJob.Reconstitute(
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            null,
            null,
            null);

        Assert.Throws<ArgumentException>(() => job.UpdateIndexingProgress("Indexing page 1 of 3"));

        Assert.Equal(status, job.Status);
        Assert.Equal(string.Empty, job.Progress);
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

    [Theory]
    [InlineData(IndexingJobStatus.Pending)]
    [InlineData(IndexingJobStatus.Completed)]
    [InlineData(IndexingJobStatus.Failed)]
    public void Complete_NonRunningAttempt_RejectsTransitionWithoutChangingAttempt(IndexingJobStatus status)
    {
        var job = CreateJob();
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var completedAt = startedAt.AddMinutes(2);
        if (status != IndexingJobStatus.Pending)
        {
            job.Start(startedAt);
        }

        if (status == IndexingJobStatus.Completed)
        {
            job.Complete(completedAt);
        }
        else if (status == IndexingJobStatus.Failed)
        {
            job.Fail("Indexing failed.", completedAt);
        }

        var originalStartedAt = job.StartedAt;
        var originalCompletedAt = job.CompletedAt;

        Assert.Throws<ArgumentException>(() => job.Complete(completedAt.AddMinutes(1)));

        Assert.Equal(status, job.Status);
        Assert.Equal(originalStartedAt, job.StartedAt);
        Assert.Equal(originalCompletedAt, job.CompletedAt);
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
    [InlineData(IndexingJobStatus.Pending)]
    [InlineData(IndexingJobStatus.Completed)]
    [InlineData(IndexingJobStatus.Failed)]
    public void Fail_NonRunningAttempt_RejectsTransitionWithoutChangingAttempt(IndexingJobStatus status)
    {
        var job = CreateJob();
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var completedAt = startedAt.AddMinutes(2);
        if (status != IndexingJobStatus.Pending)
        {
            job.Start(startedAt);
        }

        if (status == IndexingJobStatus.Completed)
        {
            job.Complete(completedAt);
        }
        else if (status == IndexingJobStatus.Failed)
        {
            job.Fail("Previous failure.", completedAt);
        }

        var originalStartedAt = job.StartedAt;
        var originalCompletedAt = job.CompletedAt;
        var originalFailureMessage = job.FailureMessage;

        Assert.Throws<ArgumentException>(() => job.Fail(
            "New failure.",
            completedAt.AddMinutes(1)));

        Assert.Equal(status, job.Status);
        Assert.Equal(originalStartedAt, job.StartedAt);
        Assert.Equal(originalCompletedAt, job.CompletedAt);
        Assert.Equal(originalFailureMessage, job.FailureMessage);
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
