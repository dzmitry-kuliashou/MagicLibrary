using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.ValueObjects;
using MagicLibrary.Infrastructure.StorageModels.Documents.Mappers;
using Xunit;

namespace MagicLibrary.UnitTests.Infrastructure.StorageModels.Documents;

public sealed class DocumentMapperTests
{
    [Fact]
    public void MapToDocumentAndBack_Book_PreservesCurrentState()
    {
        var original = CreateBook();

        var document = BookDocumentMapper.ToDocument(original);
        var restored = BookDocumentMapper.ToAggregate(document);

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.SourceFileName, restored.SourceFileName);
        Assert.Equal(original.Title, restored.Title);
        Assert.Equal(original.Author, restored.Author);
        Assert.Equal(original.Keywords.Count, restored.Keywords.Count);
        Assert.All(original.Keywords, keyword => Assert.Contains(keyword, restored.Keywords));
        Assert.Equal(original.ContentHash, restored.ContentHash);
        Assert.Equal(original.AddedAt, restored.AddedAt);
        Assert.Equal(original.AddedAt.Offset, restored.AddedAt.Offset);
        Assert.Equal(original.IndexedAt, restored.IndexedAt);
        Assert.Equal(original.IndexedAt!.Value.Offset, restored.IndexedAt!.Value.Offset);
        Assert.Equal(original.IndexingStatus, restored.IndexingStatus);
    }

    [Fact]
    public void MapToDocumentAndBack_IndexingJob_PreservesCurrentState()
    {
        var original = CreateFailedIndexingJob();

        var document = IndexingJobDocumentMapper.ToDocument(original);
        var restored = IndexingJobDocumentMapper.ToAggregate(document);

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.BookId, restored.BookId);
        Assert.Equal(original.Status, restored.Status);
        Assert.Equal(original.StartedAt, restored.StartedAt);
        Assert.Equal(original.StartedAt!.Value.Offset, restored.StartedAt!.Value.Offset);
        Assert.Equal(original.CompletedAt, restored.CompletedAt);
        Assert.Equal(original.CompletedAt!.Value.Offset, restored.CompletedAt!.Value.Offset);
        Assert.Equal(original.FailureMessage, restored.FailureMessage);
        Assert.Equal(original.Progress, restored.Progress);
        Assert.Collection(
            restored.FileErrors,
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

    [Fact]
    public void MapToDocumentAndBack_IndexingJobWithoutBookId_PreservesLibraryScope()
    {
        var original = IndexingJob.Create(null);

        var document = IndexingJobDocumentMapper.ToDocument(original);
        var restored = IndexingJobDocumentMapper.ToAggregate(document);

        Assert.Null(document.BookId);
        Assert.Null(restored.BookId);
    }

    [Fact]
    public void MapToDocumentAndBack_Conversation_PreservesCurrentState()
    {
        var original = CreateConversation();

        var document = ConversationDocumentMapper.ToDocument(original);
        var restored = ConversationDocumentMapper.ToAggregate(document);

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.CreatedAt, restored.CreatedAt);
        Assert.Equal(original.CreatedAt.Offset, restored.CreatedAt.Offset);
        Assert.Equal(original.UpdatedAt, restored.UpdatedAt);
        Assert.Equal(original.UpdatedAt.Offset, restored.UpdatedAt.Offset);
        Assert.Equal(original.Messages.Count, restored.Messages.Count);
        for (var messageIndex = 0; messageIndex < original.Messages.Count; messageIndex++)
        {
            var originalMessage = original.Messages[messageIndex];
            var restoredMessage = restored.Messages[messageIndex];

            Assert.Equal(originalMessage.Role, restoredMessage.Role);
            Assert.Equal(originalMessage.Content, restoredMessage.Content);
            Assert.Equal(originalMessage.Status, restoredMessage.Status);
            Assert.Equal(originalMessage.SourceReferences.Count, restoredMessage.SourceReferences.Count);
            for (var referenceIndex = 0; referenceIndex < originalMessage.SourceReferences.Count; referenceIndex++)
            {
                var originalReference = originalMessage.SourceReferences[referenceIndex];
                var restoredReference = restoredMessage.SourceReferences[referenceIndex];

                Assert.Equal(originalReference.BookId, restoredReference.BookId);
                Assert.Equal(originalReference.BookTitle, restoredReference.BookTitle);
                Assert.Equal(originalReference.PageNumber, restoredReference.PageNumber);
                Assert.Equal(originalReference.ChunkId, restoredReference.ChunkId);
            }
        }
    }

    [Fact]
    public void MapToDocumentAndBack_ConversationPreservesMessageStatuses()
    {
        foreach (var status in Enum.GetValues<MessageStatus>())
        {
            var original = CreateConversation(status);

            var document = ConversationDocumentMapper.ToDocument(original);
            var restored = ConversationDocumentMapper.ToAggregate(document);

            Assert.Equal(status, restored.Messages[^1].Status);
            Assert.Equal(status == MessageStatus.Completed ? "Answer" : string.Empty, restored.Messages[^1].Content);
        }
    }

    private static Book CreateBook()
    {
        var book = Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

        book.AddKeywords(["semantic", "search"]);
        book.MarkIndexingStarted();
        book.MarkIndexed(new DateTimeOffset(2026, 1, 2, 3, 5, 5, TimeSpan.Zero));

        return book;
    }

    private static IndexingJob CreateFailedIndexingJob()
    {
        var job = IndexingJob.Create(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        job.Start(startedAt);
        job.UpdateIndexingProgress("Indexing page 1 of 3");
        job.AddFileError("first.pdf", "First error.");
        job.AddFileError("second.pdf", "Second error.");
        job.Fail("Indexing failed.", startedAt.AddMinutes(2));

        return job;
    }

    private static Conversation CreateConversation(MessageStatus assistantStatus = MessageStatus.Completed)
    {
        var createdAt = new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);
        var conversation = Conversation.Create("Research", createdAt, "System instructions");
        conversation.AddUserMessage("Question", createdAt.AddMinutes(1));
        conversation.BeginAssistantResponse(createdAt.AddMinutes(2));

        switch (assistantStatus)
        {
            case MessageStatus.Pending:
                break;
            case MessageStatus.Completed:
                conversation.CompleteAssistantResponse(
                    "Answer",
                    [
                        SourceReference.Create(
                            Guid.Parse("11111111-1111-1111-1111-111111111111"),
                            "Book title",
                            4,
                            Guid.Parse("22222222-2222-2222-2222-222222222222"))
                    ],
                    createdAt.AddMinutes(3));
                break;
            case MessageStatus.Failed:
                conversation.FailAssistantResponse(createdAt.AddMinutes(3));
                break;
            case MessageStatus.Interrupted:
                conversation.InterruptAssistantResponse(createdAt.AddMinutes(3));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(assistantStatus), assistantStatus, null);
        }

        return conversation;
    }
}
