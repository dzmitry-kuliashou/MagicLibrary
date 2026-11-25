using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Application.Indexing;
using MagicLibrary.Application.KeywordExtraction;
using MagicLibrary.Application.PdfProcessing;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Indexing;

public sealed class LibraryIndexingScenarioTests
{
    [Fact]
    public async Task Execute_NewPdf_CreatesBookWithExtractedMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string fileName = "new-book.pdf";
        const string title = "Extracted title";
        const string author = "Extracted author";
        var filePath = Path.Combine(temporaryDirectory.Path, fileName);
        await File.WriteAllTextAsync(filePath, "PDF fixture");

        var extraction = new PdfTextExtractionResult(
            [new PdfTextExtractionPage(1, "Extracted page text")],
            title,
            author);
        var extractor = new Mock<IPdfTextExtractor>();
        extractor
            .Setup(service => service.ExtractAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        var bookReadRepository = new Mock<IBookReadRepository>();
        bookReadRepository
            .Setup(repository => repository.FindByFileName(
                fileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Book?)null);

        var createdBook = Book.Create(
            fileName,
            title,
            author,
            "content-hash",
            DateTimeOffset.UtcNow);
        var sender = new Mock<ISender>();
        ConfigureSuccessfulIndexing(sender, createdBook);
        sender
            .Setup(service => service.Send(
                It.IsAny<CreateBookCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateBookCommand _, CancellationToken _) => createdBook);

        var scenario = CreateScenario(
            temporaryDirectory.Path,
            extractor.Object,
            bookReadRepository.Object,
            sender.Object);

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(null),
            CancellationToken.None);

        Assert.False(result.IsError);
        sender.Verify(service => service.Send(
            It.Is<CreateBookCommand>(command =>
                command.FileName == fileName
                && command.Title == title
                && command.Author == author),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Execute_CaseVariantFilename_ReusesPendingBook()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string scannedFileName = "NEW-BOOK.PDF";
        const string existingFileName = "new-book.pdf";
        var filePath = Path.Combine(temporaryDirectory.Path, scannedFileName);
        await File.WriteAllTextAsync(filePath, "PDF fixture");

        var extractor = new Mock<IPdfTextExtractor>();
        extractor
            .Setup(service => service.ExtractAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfTextExtractionResult(
                [new PdfTextExtractionPage(1, "Extracted page text")],
                "Extracted title",
                "Extracted author"));

        var existingBook = Book.Create(
            existingFileName,
            "Existing title",
            "Existing author",
            "content-hash",
            DateTimeOffset.UtcNow);
        var bookReadRepository = new Mock<IBookReadRepository>();
        bookReadRepository
            .Setup(repository => repository.FindByFileName(
                scannedFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBook);

        var sender = new Mock<ISender>();
        ConfigureSuccessfulIndexing(sender, existingBook);
        var scenario = CreateScenario(
            temporaryDirectory.Path,
            extractor.Object,
            bookReadRepository.Object,
            sender.Object);

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(null),
            CancellationToken.None);

        Assert.False(result.IsError);
        bookReadRepository.Verify(repository => repository.FindByFileName(
            scannedFileName,
            CancellationToken.None), Times.Once);
        sender.Verify(service => service.Send(
            It.IsAny<CreateBookCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(BookIndexingStatus.Indexed)]
    [InlineData(BookIndexingStatus.Failed)]
    [InlineData(BookIndexingStatus.InProgress)]
    public async Task Execute_NonPendingBook_SkipsWithoutErrorAndContinues(
        BookIndexingStatus skippedStatus)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string skippedFileName = "already-processed.pdf";
        const string pendingFileName = "pending-book.pdf";
        var skippedFilePath = Path.Combine(temporaryDirectory.Path, skippedFileName);
        var pendingFilePath = Path.Combine(temporaryDirectory.Path, pendingFileName);
        await File.WriteAllTextAsync(skippedFilePath, "PDF fixture");
        await File.WriteAllTextAsync(pendingFilePath, "PDF fixture");

        var extractor = new Mock<IPdfTextExtractor>();
        extractor
            .Setup(service => service.ExtractAsync(
                pendingFilePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfTextExtractionResult(
                [new PdfTextExtractionPage(1, "Last page")],
                "Pending title",
                "Pending author"));

        var skippedBook = Book.Create(
            skippedFileName,
            "Skipped title",
            "Skipped author",
            "skipped-hash",
            DateTimeOffset.UtcNow);
        switch (skippedStatus)
        {
            case BookIndexingStatus.Indexed:
                skippedBook.MarkIndexingStarted();
                skippedBook.MarkIndexed(DateTimeOffset.UtcNow);
                break;
            case BookIndexingStatus.Failed:
                skippedBook.MarkIndexingFailed();
                break;
            case BookIndexingStatus.InProgress:
                skippedBook.MarkIndexingStarted();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(skippedStatus), skippedStatus, null);
        }

        var pendingBook = Book.Create(
            pendingFileName,
            "Pending title",
            "Pending author",
            "pending-hash",
            DateTimeOffset.UtcNow);
        var bookReadRepository = new Mock<IBookReadRepository>();
        bookReadRepository
            .Setup(repository => repository.FindByFileName(
                skippedFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(skippedBook);
        bookReadRepository
            .Setup(repository => repository.FindByFileName(
                pendingFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingBook);

        var sender = new Mock<ISender>();
        ConfigureSuccessfulIndexing(sender, pendingBook);
        var scenario = CreateScenario(
            temporaryDirectory.Path,
            extractor.Object,
            bookReadRepository.Object,
            sender.Object);

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(null),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(skippedStatus, skippedBook.IndexingStatus);
        Assert.Equal(BookIndexingStatus.Indexed, pendingBook.IndexingStatus);
        extractor.Verify(service => service.ExtractAsync(
            skippedFilePath,
            CancellationToken.None), Times.Never);
        extractor.Verify(service => service.ExtractAsync(
            pendingFilePath,
            CancellationToken.None), Times.Once);
        sender.Verify(service => service.Send(
            It.Is<MarkBookIndexingStartedCommand>(command =>
                command.BookId == pendingBook.Id),
            CancellationToken.None), Times.Once);
        sender.Verify(service => service.Send(
            It.Is<MarkBookIndexingStartedCommand>(command =>
                command.BookId == skippedBook.Id),
            It.IsAny<CancellationToken>()), Times.Never);
        sender.Verify(service => service.Send(
            It.IsAny<AddIndexingJobFileErrorCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_EmbeddingFailure_MarksBookFailedAndContinues()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string failedFileName = "failed-book.pdf";
        const string successfulFileName = "successful-book.pdf";
        var failedFilePath = Path.Combine(temporaryDirectory.Path, failedFileName);
        var successfulFilePath = Path.Combine(temporaryDirectory.Path, successfulFileName);
        await File.WriteAllTextAsync(failedFilePath, "PDF fixture");
        await File.WriteAllTextAsync(successfulFilePath, "PDF fixture");

        var extractor = new Mock<IPdfTextExtractor>();
        extractor
            .Setup(service => service.ExtractAsync(
                failedFilePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfTextExtractionResult(
                [new PdfTextExtractionPage(1, "Failing page")],
                "Failed title",
                "Failed author"));
        extractor
            .Setup(service => service.ExtractAsync(
                successfulFilePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfTextExtractionResult(
                [new PdfTextExtractionPage(1, "Last page")],
                "Successful title",
                "Successful author"));

        var failedBook = Book.Create(
            failedFileName,
            "Failed title",
            "Failed author",
            "failed-hash",
            DateTimeOffset.UtcNow);
        var successfulBook = Book.Create(
            successfulFileName,
            "Successful title",
            "Successful author",
            "successful-hash",
            DateTimeOffset.UtcNow);
        var bookReadRepository = new Mock<IBookReadRepository>();
        bookReadRepository
            .Setup(repository => repository.FindByFileName(
                failedFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedBook);
        bookReadRepository
            .Setup(repository => repository.FindByFileName(
                successfulFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(successfulBook);

        var sender = new Mock<ISender>();
        ConfigureSuccessfulIndexing(sender, failedBook, successfulBook);
        var scenario = CreateScenario(
            temporaryDirectory.Path,
            extractor.Object,
            bookReadRepository.Object,
            sender.Object,
            "Failing page");

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(null),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(BookIndexingStatus.Failed, failedBook.IndexingStatus);
        Assert.Equal(BookIndexingStatus.Indexed, successfulBook.IndexingStatus);
        sender.Verify(service => service.Send(
            It.Is<MarkBookIndexingFailedCommand>(command =>
                command.BookId == failedBook.Id),
            CancellationToken.None), Times.Once);
        sender.Verify(service => service.Send(
            It.Is<AddIndexingJobFileErrorCommand>(command =>
                command.FileName == failedFileName
                && command.Message.Contains("embedding failed")),
            CancellationToken.None), Times.Once);
        sender.Verify(service => service.Send(
            It.Is<ReplaceDocumentChunksCommand>(command =>
                command.BookId == successfulBook.Id),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Execute_KeywordExtractionTimeout_MarksBookFailedAndContinues()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string fileName = "timed-out-book.pdf";
        const string pageText = "Page requiring keyword extraction";
        var filePath = Path.Combine(temporaryDirectory.Path, fileName);
        await File.WriteAllTextAsync(filePath, "PDF fixture");

        var extractor = new Mock<IPdfTextExtractor>();
        extractor
            .Setup(service => service.ExtractAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfTextExtractionResult(
                [new PdfTextExtractionPage(1, pageText)],
                "Timed out title",
                "Timed out author"));

        var book = Book.Create(
            fileName,
            "Timed out title",
            "Timed out author",
            "timeout-hash",
            DateTimeOffset.UtcNow);
        var bookReadRepository = new Mock<IBookReadRepository>();
        bookReadRepository
            .Setup(repository => repository.FindByFileName(
                fileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(book);

        var sender = new Mock<ISender>();
        ConfigureSuccessfulIndexing(sender, book);
        var scenario = CreateScenario(
            temporaryDirectory.Path,
            extractor.Object,
            bookReadRepository.Object,
            sender.Object,
            failingKeywordText: pageText);

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(null),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(BookIndexingStatus.Failed, book.IndexingStatus);
        sender.Verify(service => service.Send(
            It.Is<AddIndexingJobFileErrorCommand>(command =>
                command.FileName == fileName &&
                command.Message.Contains("The keyword extraction request timed out.")),
            CancellationToken.None), Times.Once);
        sender.Verify(service => service.Send(
            It.IsAny<MarkBookIndexedCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ProgressCountsPendingAttemptsAndContinuesWhenProgressUpdateFails()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string successfulFileName = "successful-book.pdf";
        const string failedFileName = "failed-book.pdf";
        const string skippedFileName = "already-indexed-book.pdf";
        var successfulFilePath = Path.Combine(temporaryDirectory.Path, successfulFileName);
        var failedFilePath = Path.Combine(temporaryDirectory.Path, failedFileName);
        var skippedFilePath = Path.Combine(temporaryDirectory.Path, skippedFileName);
        await File.WriteAllTextAsync(successfulFilePath, "PDF fixture");
        await File.WriteAllTextAsync(failedFilePath, "PDF fixture");
        await File.WriteAllTextAsync(skippedFilePath, "PDF fixture");

        var extractor = new Mock<IPdfTextExtractor>();
        extractor
            .Setup(service => service.ExtractAsync(
                successfulFilePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfTextExtractionResult(
                [new PdfTextExtractionPage(1, "Successful page")],
                "Successful title",
                "Successful author"));
        extractor
            .Setup(service => service.ExtractAsync(
                failedFilePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfTextExtractionResult(
                [new PdfTextExtractionPage(1, "Failing page")],
                "Failed title",
                "Failed author"));
        extractor
            .Setup(service => service.ExtractAsync(
                skippedFilePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfTextExtractionResult(
                [new PdfTextExtractionPage(1, "Skipped page")],
                "Skipped title",
                "Skipped author"));

        var successfulBook = Book.Create(
            successfulFileName,
            "Successful title",
            "Successful author",
            "successful-hash",
            DateTimeOffset.UtcNow);
        var failedBook = Book.Create(
            failedFileName,
            "Failed title",
            "Failed author",
            "failed-hash",
            DateTimeOffset.UtcNow);
        var skippedBook = Book.Create(
            skippedFileName,
            "Skipped title",
            "Skipped author",
            "skipped-hash",
            DateTimeOffset.UtcNow);
        skippedBook.MarkIndexingStarted();
        skippedBook.MarkIndexed(DateTimeOffset.UtcNow);

        var booksByFileName = new Dictionary<string, Book>
        {
            [successfulFileName] = successfulBook,
            [failedFileName] = failedBook,
            [skippedFileName] = skippedBook
        };
        var bookReadRepository = new Mock<IBookReadRepository>();
        bookReadRepository
            .Setup(repository => repository.FindByFileName(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string fileName, CancellationToken _) => booksByFileName[fileName]);

        var progressUpdates = new List<string>();
        var sender = new Mock<ISender>();
        ConfigureSuccessfulIndexing(sender, successfulBook, failedBook);
        sender
            .Setup(service => service.Send(
                It.IsAny<UpdateIndexingJobProgressCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((UpdateIndexingJobProgressCommand command, CancellationToken _) =>
            {
                progressUpdates.Add(command.Progress);
                if (progressUpdates.Count == 1)
                {
                    return Task.FromResult<ErrorOr<IndexingJob>>(
                        Error.Failure("IndexingJob.ProgressUpdateFailed", "Progress update failed."));
                }

                return Task.FromResult<ErrorOr<IndexingJob>>(IndexingJob.Create(null));
            });

        var scenario = CreateScenario(
            temporaryDirectory.Path,
            extractor.Object,
            bookReadRepository.Object,
            sender.Object,
            "Failing page");

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(null),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(BookIndexingStatus.Indexed, successfulBook.IndexingStatus);
        Assert.Equal(BookIndexingStatus.Failed, failedBook.IndexingStatus);
        Assert.Equal(BookIndexingStatus.Indexed, skippedBook.IndexingStatus);
        Assert.Equal(
            ["1 of 2 books indexed", "2 of 2 books indexed"],
            progressUpdates);
        sender.Verify(service => service.Send(
            It.IsAny<MarkBookIndexingStartedCommand>(),
            CancellationToken.None), Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_MultiplePages_CreatesOneChunkPerNonblankPage()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        const string fileName = "multi-page-book.pdf";
        var filePath = Path.Combine(temporaryDirectory.Path, fileName);
        await File.WriteAllTextAsync(filePath, "PDF fixture");

        var extraction = new PdfTextExtractionResult(
            [
                new PdfTextExtractionPage(2, "First page"),
                new PdfTextExtractionPage(5, "  "),
                new PdfTextExtractionPage(9, "Last page")
            ],
            "Extracted title",
            "Extracted author");
        var extractor = new Mock<IPdfTextExtractor>();
        extractor
            .Setup(service => service.ExtractAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        var book = Book.Create(
            fileName,
            "Extracted title",
            "Extracted author",
            "content-hash",
            DateTimeOffset.UtcNow);
        var bookReadRepository = new Mock<IBookReadRepository>();
        bookReadRepository
            .Setup(repository => repository.FindByFileName(
                fileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(book);

        ReplaceDocumentChunksCommand? replacedChunks = null;
        var sender = new Mock<ISender>();
        ConfigureSuccessfulIndexing(sender, book);
        sender
            .Setup(service => service.Send(
                It.IsAny<ReplaceDocumentChunksCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((ReplaceDocumentChunksCommand command, CancellationToken _) =>
            {
                replacedChunks = command;
                return Task.FromResult<ErrorOr<Success>>(Result.Success);
            });

        var scenario = CreateScenario(
            temporaryDirectory.Path,
            extractor.Object,
            bookReadRepository.Object,
            sender.Object);

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(null),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(replacedChunks);
        Assert.Equal(book.Id, replacedChunks.BookId);
        Assert.Equal(BookIndexingStatus.Indexed, book.IndexingStatus);
        Assert.Equal(new[] { "first", "last" }, book.Keywords.OrderBy(keyword => keyword));
        Assert.Collection(
            replacedChunks.Chunks,
            chunk =>
            {
                Assert.Equal(0, chunk.ChunkIndex);
                Assert.Equal(2, chunk.PageNumber);
                Assert.Equal("First page", chunk.Content);
                Assert.Equal(new[] { 1f, 2f }, chunk.Embedding);
            },
            chunk =>
            {
                Assert.Equal(1, chunk.ChunkIndex);
                Assert.Equal(9, chunk.PageNumber);
                Assert.Equal("Last page", chunk.Content);
                Assert.Equal(new[] { 3f, 4f }, chunk.Embedding);
            });
        sender.Verify(service => service.Send(
            It.Is<AddBookKeywordsCommand>(command =>
                command.BookId == book.Id
                && command.Keywords.SequenceEqual(new[] { "first", "last" })),
            CancellationToken.None), Times.Once);
        sender.Verify(service => service.Send(
            It.Is<MarkBookIndexedCommand>(command => command.BookId == book.Id),
            CancellationToken.None), Times.Once);
    }

    private static LibraryIndexingScenario CreateScenario(
        string rootDirectory,
        IPdfTextExtractor extractor,
        IBookReadRepository bookReadRepository,
        ISender sender,
        string? failingEmbeddingText = null,
        string? failingKeywordText = null)
    {
        var embeddingGenerator = new Mock<IEmbeddingGenerator>();
        embeddingGenerator
            .Setup(generator => generator.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((string text, CancellationToken _) =>
                text == failingEmbeddingText
                    ? Task.FromException<IReadOnlyList<float>>(
                        new InvalidOperationException("embedding failed"))
                    : Task.FromResult<IReadOnlyList<float>>(
                        text == "First page" ? [1f, 2f] : [3f, 4f]));

        var keywordExtractor = new Mock<IKeywordExtractor>();
        keywordExtractor
            .Setup(extractor => extractor.ExtractAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((string text, CancellationToken _) =>
                text == failingKeywordText
                    ? Task.FromException<IReadOnlyList<string>>(
                        new TaskCanceledException("The keyword extraction request timed out."))
                    : Task.FromResult<IReadOnlyList<string>>(
                        text == "First page" ? ["first"] : ["last"]));

        return new LibraryIndexingScenario(
            Options.Create(new BookStorageOptions { RootDirectory = rootDirectory }),
            extractor,
            bookReadRepository,
            embeddingGenerator.Object,
            keywordExtractor.Object,
            sender);
    }

    private static void ConfigureSuccessfulIndexing(Mock<ISender> sender, params Book[] books)
    {
        var booksById = books.ToDictionary(book => book.Id);
        sender
            .Setup(service => service.Send(
                It.IsAny<MarkBookIndexingStartedCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarkBookIndexingStartedCommand command, CancellationToken _) =>
            {
                var book = booksById[command.BookId];
                book.MarkIndexingStarted();
                return book;
            });
        sender
            .Setup(service => service.Send(
                It.IsAny<AddBookKeywordsCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((AddBookKeywordsCommand command, CancellationToken _) =>
            {
                var book = booksById[command.BookId];
                book.AddKeywords(command.Keywords);
                return book;
            });
        sender
            .Setup(service => service.Send(
                It.IsAny<ReplaceDocumentChunksCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((ReplaceDocumentChunksCommand _, CancellationToken _) =>
                Task.FromResult<ErrorOr<Success>>(Result.Success));
        sender
            .Setup(service => service.Send(
                It.IsAny<MarkBookIndexedCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarkBookIndexedCommand command, CancellationToken _) =>
            {
                var book = booksById[command.BookId];
                book.MarkIndexed(command.IndexedAt);
                return book;
            });
        sender
            .Setup(service => service.Send(
                It.IsAny<MarkBookIndexingFailedCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarkBookIndexingFailedCommand command, CancellationToken _) =>
            {
                var book = booksById[command.BookId];
                book.MarkIndexingFailed();
                return book;
            });
        sender
            .Setup(service => service.Send(
                It.IsAny<AddIndexingJobFileErrorCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((AddIndexingJobFileErrorCommand _, CancellationToken _) =>
                IndexingJob.Create(null));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"MagicLibrary-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
