using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Application.Errors;
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

public sealed class BookIndexingScenarioTests
{
    [Fact]
    public async Task Execute_BookDoesNotExist_ReturnsNotFound()
    {
        using var fixture = new ScenarioFixture();
        var bookId = Guid.NewGuid();
        var scenario = fixture.CreateScenario();

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(bookId),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(BookErrors.BookNotFound(bookId).Code, error.Code);
        fixture.BookReadRepository.Verify(
            repository => repository.GetByIdAsync(bookId, CancellationToken.None),
            Times.Once);
        fixture.PdfTextExtractor.Verify(
            extractor => extractor.ExtractAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<ResetBookIndexingCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<MarkBookIndexingStartedCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_PdfDoesNotExist_ReturnsFailureWithoutResetting()
    {
        using var fixture = new ScenarioFixture();
        var book = Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var indexedAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        book.MarkIndexingStarted();
        book.MarkIndexed(indexedAt);
        book.AddKeywords(["semantic", "search"]);
        fixture.ConfigureSuccessfulCommands(book);
        var scenario = fixture.CreateScenario();

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(book.Id),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(BookIndexingStatus.Indexed, book.IndexingStatus);
        Assert.Equal(indexedAt, book.IndexedAt);
        Assert.Equal(2, book.Keywords.Count);
        Assert.Contains("semantic", book.Keywords);
        Assert.Contains("search", book.Keywords);
        fixture.BookReadRepository.Verify(
            repository => repository.GetByIdAsync(book.Id, CancellationToken.None),
            Times.Once);
        fixture.PdfTextExtractor.Verify(
            extractor => extractor.ExtractAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<ResetBookIndexingCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<MarkBookIndexingStartedCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_PdfExtractionFails_ReturnsFailureWithoutResetting(bool cancellationRequested)
    {
        using var fixture = new ScenarioFixture();
        using var cancellationTokenSource = new CancellationTokenSource();
        var book = Book.Create(
            "book.pdf",
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var indexedAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        book.MarkIndexingStarted();
        book.MarkIndexed(indexedAt);
        book.AddKeywords(["semantic", "search"]);
        fixture.ConfigureSuccessfulCommands(book);
        var filePath = Path.Combine(fixture.Storage.Path, book.SourceFileName);
        await File.WriteAllTextAsync(filePath, "Invalid PDF fixture");
        fixture.PdfTextExtractor
            .Setup(extractor => extractor.ExtractAsync(
                filePath,
                It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken token) =>
            {
                if (cancellationRequested)
                {
                    cancellationTokenSource.Cancel();
                    return Task.FromCanceled<PdfTextExtractionResult>(token);
                }

                return Task.FromException<PdfTextExtractionResult>(
                    new InvalidDataException("Invalid PDF."));
            });
        var scenario = fixture.CreateScenario();

        if (cancellationRequested)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await scenario.ExecuteAsync(
                    IndexingJob.Create(book.Id),
                    cancellationTokenSource.Token));
        }
        else
        {
            var result = await scenario.ExecuteAsync(
                IndexingJob.Create(book.Id),
                cancellationTokenSource.Token);
            Assert.True(result.IsError);
        }

        Assert.Equal(BookIndexingStatus.Indexed, book.IndexingStatus);
        Assert.Equal(indexedAt, book.IndexedAt);
        Assert.Equal(2, book.Keywords.Count);
        Assert.Contains("semantic", book.Keywords);
        Assert.Contains("search", book.Keywords);
        fixture.BookReadRepository.Verify(
            repository => repository.GetByIdAsync(book.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.PdfTextExtractor.Verify(
            extractor => extractor.ExtractAsync(filePath, It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<ResetBookIndexingCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<MarkBookIndexingStartedCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_MultipleUsablePages_IndexesOnlyRequestedBookAndUpdatesPageProgress()
    {
        using var fixture = new ScenarioFixture();
        const string fileName = "multi-page-book.pdf";
        const string firstPageText = "First usable page";
        const string lastPageText = "Last usable page";
        var filePath = Path.Combine(fixture.Storage.Path, fileName);
        await File.WriteAllTextAsync(filePath, "PDF fixture");
        var extraction = new PdfTextExtractionResult(
            [
                new PdfTextExtractionPage(2, firstPageText),
                new PdfTextExtractionPage(5, "  "),
                new PdfTextExtractionPage(9, lastPageText)
            ],
            "Title",
            "Author");
        fixture.ConfigureExtraction(filePath, extraction);
        var book = Book.Create(
            fileName,
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        fixture.ConfigureSuccessfulCommands(book);
        fixture.EmbeddingGenerator
            .Setup(generator => generator.GenerateAsync(firstPageText, CancellationToken.None))
            .ReturnsAsync((IReadOnlyList<float>)[1f, 2f]);
        fixture.EmbeddingGenerator
            .Setup(generator => generator.GenerateAsync(lastPageText, CancellationToken.None))
            .ReturnsAsync((IReadOnlyList<float>)[3f, 4f]);
        fixture.KeywordExtractor
            .Setup(extractor => extractor.ExtractAsync(firstPageText, CancellationToken.None))
            .ReturnsAsync((IReadOnlyList<string>)["first"]);
        fixture.KeywordExtractor
            .Setup(extractor => extractor.ExtractAsync(lastPageText, CancellationToken.None))
            .ReturnsAsync((IReadOnlyList<string>)["last"]);
        ReplaceDocumentChunksCommand? replacedChunks = null;
        fixture.Sender
            .Setup(sender => sender.Send(
                It.IsAny<ReplaceDocumentChunksCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((ReplaceDocumentChunksCommand command, CancellationToken _) =>
            {
                replacedChunks = command;
                return Task.FromResult<ErrorOr<Success>>(Result.Success);
            });
        var scenario = fixture.CreateScenario();

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(book.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(BookIndexingStatus.Indexed, book.IndexingStatus);
        Assert.Equal(new[] { "first", "last" }, book.Keywords.OrderBy(keyword => keyword));
        Assert.NotNull(replacedChunks);
        Assert.Equal(book.Id, replacedChunks.BookId);
        Assert.Collection(
            replacedChunks.Chunks,
            chunk =>
            {
                Assert.Equal(0, chunk.ChunkIndex);
                Assert.Equal(2, chunk.PageNumber);
                Assert.Equal(firstPageText, chunk.Content);
                Assert.Equal(new[] { 1f, 2f }, chunk.Embedding);
            },
            chunk =>
            {
                Assert.Equal(1, chunk.ChunkIndex);
                Assert.Equal(9, chunk.PageNumber);
                Assert.Equal(lastPageText, chunk.Content);
                Assert.Equal(new[] { 3f, 4f }, chunk.Embedding);
            });
        fixture.EmbeddingGenerator.Verify(
            generator => generator.GenerateAsync(firstPageText, CancellationToken.None),
            Times.Once);
        fixture.EmbeddingGenerator.Verify(
            generator => generator.GenerateAsync(lastPageText, CancellationToken.None),
            Times.Once);
        fixture.EmbeddingGenerator.Verify(
            generator => generator.GenerateAsync(
                It.Is<string>(text => string.IsNullOrWhiteSpace(text)),
                CancellationToken.None),
            Times.Never);
        fixture.KeywordExtractor.Verify(
            extractor => extractor.ExtractAsync(firstPageText, CancellationToken.None),
            Times.Once);
        fixture.KeywordExtractor.Verify(
            extractor => extractor.ExtractAsync(lastPageText, CancellationToken.None),
            Times.Once);
        fixture.KeywordExtractor.Verify(
            extractor => extractor.ExtractAsync(
                It.Is<string>(text => string.IsNullOrWhiteSpace(text)),
                CancellationToken.None),
            Times.Never);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.Is<AddBookKeywordsCommand>(command =>
                    command.BookId == book.Id
                    && command.Keywords.SequenceEqual(new[] { "first", "last" })),
                CancellationToken.None),
            Times.Once);
        var progress = fixture.Sender.Invocations
            .Select(invocation => invocation.Arguments[0])
            .OfType<UpdateIndexingJobProgressCommand>()
            .Select(command => command.Progress);
        Assert.Equal(
            new[] { "1 of 2 pages indexed", "2 of 2 pages indexed" },
            progress);
        fixture.BookReadRepository.Verify(
            repository => repository.GetByIdAsync(book.Id, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Execute_NoUsablePages_ReplacesWithEmptyChunksAndMarksIndexed()
    {
        using var fixture = new ScenarioFixture();
        const string fileName = "empty-book.pdf";
        var filePath = Path.Combine(fixture.Storage.Path, fileName);
        await File.WriteAllTextAsync(filePath, "PDF fixture");
        fixture.ConfigureExtraction(
            filePath,
            new PdfTextExtractionResult(
                [
                    new PdfTextExtractionPage(1, ""),
                    new PdfTextExtractionPage(2, "  ")
                ],
                "Title",
                "Author"));
        var book = Book.Create(
            fileName,
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        fixture.ConfigureSuccessfulCommands(book);
        ReplaceDocumentChunksCommand? replacedChunks = null;
        fixture.Sender
            .Setup(sender => sender.Send(
                It.IsAny<ReplaceDocumentChunksCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((ReplaceDocumentChunksCommand command, CancellationToken _) =>
            {
                replacedChunks = command;
                return Task.FromResult<ErrorOr<Success>>(Result.Success);
            });
        var scenario = fixture.CreateScenario();

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(book.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(BookIndexingStatus.Indexed, book.IndexingStatus);
        Assert.Empty(book.Keywords);
        Assert.NotNull(replacedChunks);
        Assert.Equal(book.Id, replacedChunks.BookId);
        Assert.Empty(replacedChunks.Chunks);
        fixture.EmbeddingGenerator.Verify(
            generator => generator.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.KeywordExtractor.Verify(
            extractor => extractor.ExtractAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.DoesNotContain(
            fixture.Sender.Invocations.Select(invocation => invocation.Arguments[0]),
            command => command is UpdateIndexingJobProgressCommand);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.Is<AddBookKeywordsCommand>(command =>
                    command.BookId == book.Id && !command.Keywords.Any()),
                CancellationToken.None),
            Times.Once);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.Is<MarkBookIndexedCommand>(command => command.BookId == book.Id),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Execute_EmbeddingFails_MarksBookFailedWithoutReplacingOldChunks()
    {
        using var fixture = new ScenarioFixture();
        const string fileName = "book.pdf";
        const string failingPage = "Page with failed embedding";
        var filePath = Path.Combine(fixture.Storage.Path, fileName);
        await File.WriteAllTextAsync(filePath, "PDF fixture");
        fixture.ConfigureExtraction(
            filePath,
            new PdfTextExtractionResult(
                [
                    new PdfTextExtractionPage(1, "First page"),
                    new PdfTextExtractionPage(2, failingPage)
                ],
                "Title",
                "Author"));
        var book = Book.Create(
            fileName,
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        book.MarkIndexingStarted();
        book.MarkIndexed(new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero));
        book.AddKeywords(["old-keyword"]);
        fixture.ConfigureSuccessfulCommands(book);
        fixture.EmbeddingGenerator
            .Setup(generator => generator.GenerateAsync(failingPage, CancellationToken.None))
            .Returns(Task.FromException<IReadOnlyList<float>>(
                new InvalidOperationException("embedding failed")));
        var scenario = fixture.CreateScenario();
        var indexingJob = IndexingJob.Create(book.Id);

        var result = await scenario.ExecuteAsync(indexingJob, CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal("IndexingJob.BookIndexingFailed", error.Code);
        Assert.Equal(BookIndexingStatus.Failed, book.IndexingStatus);
        Assert.Null(book.IndexedAt);
        Assert.Empty(book.Keywords);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.Is<MarkBookIndexingFailedCommand>(command => command.BookId == book.Id),
                CancellationToken.None),
            Times.Once);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.Is<AddIndexingJobFileErrorCommand>(command =>
                    command.IndexingJobId == indexingJob.Id
                    && command.FileName == fileName
                    && command.Message.Contains("embedding failed")),
                CancellationToken.None),
            Times.Once);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<ReplaceDocumentChunksCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<AddBookKeywordsCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<MarkBookIndexedCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ProgressUpdateFails_ContinuesIndexing()
    {
        using var fixture = new ScenarioFixture();
        const string fileName = "book.pdf";
        var filePath = Path.Combine(fixture.Storage.Path, fileName);
        await File.WriteAllTextAsync(filePath, "PDF fixture");
        fixture.ConfigureExtraction(
            filePath,
            new PdfTextExtractionResult(
                [
                    new PdfTextExtractionPage(1, "First page"),
                    new PdfTextExtractionPage(3, "Last page")
                ],
                "Title",
                "Author"));
        var book = Book.Create(
            fileName,
            "Title",
            "Author",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        fixture.ConfigureSuccessfulCommands(book);
        var progressError = Error.Failure(
            "IndexingJob.ProgressUpdateFailed",
            "Progress persistence failed.");
        fixture.Sender
            .Setup(sender => sender.Send(
                It.IsAny<UpdateIndexingJobProgressCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((UpdateIndexingJobProgressCommand _, CancellationToken _) =>
                Task.FromResult<ErrorOr<IndexingJob>>(progressError));
        var scenario = fixture.CreateScenario();

        var result = await scenario.ExecuteAsync(
            IndexingJob.Create(book.Id),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(BookIndexingStatus.Indexed, book.IndexingStatus);
        Assert.Contains("keyword", book.Keywords);
        fixture.EmbeddingGenerator.Verify(
            generator => generator.GenerateAsync(
                It.IsAny<string>(),
                CancellationToken.None),
            Times.Exactly(2));
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<ReplaceDocumentChunksCommand>(),
                CancellationToken.None),
            Times.Once);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.Is<MarkBookIndexedCommand>(command => command.BookId == book.Id),
                CancellationToken.None),
            Times.Once);
        fixture.Sender.Verify(
            sender => sender.Send(
                It.IsAny<MarkBookIndexingFailedCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        var progress = fixture.Sender.Invocations
            .Select(invocation => invocation.Arguments[0])
            .OfType<UpdateIndexingJobProgressCommand>()
            .Select(command => command.Progress);
        Assert.Equal(
            new[] { "1 of 2 pages indexed", "2 of 2 pages indexed" },
            progress);
    }

    private sealed class ScenarioFixture : IDisposable
    {
        public ScenarioFixture()
        {
            Storage = new TemporaryDirectory();
            EmbeddingGenerator
                .Setup(generator => generator.GenerateAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<float>)[1f, 2f]);
            KeywordExtractor
                .Setup(extractor => extractor.ExtractAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<string>)["keyword"]);
        }

        public TemporaryDirectory Storage { get; }

        public Mock<IBookReadRepository> BookReadRepository { get; } = new();

        public Mock<IPdfTextExtractor> PdfTextExtractor { get; } = new();

        public Mock<IEmbeddingGenerator> EmbeddingGenerator { get; } = new();

        public Mock<IKeywordExtractor> KeywordExtractor { get; } = new();

        public Mock<ISender> Sender { get; } = new();

        public BookIndexingScenario CreateScenario() =>
            new(
                Options.Create(new BookStorageOptions { RootDirectory = Storage.Path }),
                PdfTextExtractor.Object,
                BookReadRepository.Object,
                EmbeddingGenerator.Object,
                KeywordExtractor.Object,
                Sender.Object);

        public void ConfigureExtraction(string filePath, PdfTextExtractionResult extraction)
        {
            PdfTextExtractor
                .Setup(extractor => extractor.ExtractAsync(
                    filePath,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(extraction);
        }

        public void ConfigureSuccessfulCommands(Book book)
        {
            BookReadRepository
                .Setup(repository => repository.GetByIdAsync(
                    book.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(book);
            Sender
                .Setup(sender => sender.Send(
                    It.IsAny<ResetBookIndexingCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ResetBookIndexingCommand _, CancellationToken _) =>
                {
                    book.ResetIndexing();
                    return book;
                });
            Sender
                .Setup(sender => sender.Send(
                    It.IsAny<MarkBookIndexingStartedCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MarkBookIndexingStartedCommand _, CancellationToken _) =>
                {
                    book.MarkIndexingStarted();
                    return book;
                });
            Sender
                .Setup(sender => sender.Send(
                    It.IsAny<AddBookKeywordsCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddBookKeywordsCommand command, CancellationToken _) =>
                {
                    book.AddKeywords(command.Keywords);
                    return book;
                });
            Sender
                .Setup(sender => sender.Send(
                    It.IsAny<ReplaceDocumentChunksCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success);
            Sender
                .Setup(sender => sender.Send(
                    It.IsAny<MarkBookIndexedCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MarkBookIndexedCommand command, CancellationToken _) =>
                {
                    book.MarkIndexed(command.IndexedAt);
                    return book;
                });
            Sender
                .Setup(sender => sender.Send(
                    It.IsAny<MarkBookIndexingFailedCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MarkBookIndexingFailedCommand _, CancellationToken _) =>
                {
                    book.MarkIndexingFailed();
                    return book;
                });
            Sender
                .Setup(sender => sender.Send(
                    It.IsAny<AddIndexingJobFileErrorCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddIndexingJobFileErrorCommand _, CancellationToken _) =>
                    IndexingJob.Create(null));
            Sender
                .Setup(sender => sender.Send(
                    It.IsAny<UpdateIndexingJobProgressCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((UpdateIndexingJobProgressCommand _, CancellationToken _) =>
                    IndexingJob.Create(null));
        }

        public void Dispose() => Storage.Dispose();
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
