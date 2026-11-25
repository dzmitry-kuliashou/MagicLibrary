using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Application.KeywordExtraction;
using MagicLibrary.Application.PdfProcessing;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;
using Microsoft.Extensions.Options;

namespace MagicLibrary.Application.Indexing;

public sealed class LibraryIndexingScenario(
    IOptions<BookStorageOptions> storageOptions,
    IPdfTextExtractor pdfTextExtractor,
    IBookReadRepository bookReadRepository,
    IEmbeddingGenerator embeddingGenerator,
    IKeywordExtractor keywordExtractor,
    ISender sender) : IIndexingScenario
{
    public bool CanProcess(IndexingJob indexingJob) => indexingJob.BookId is null;

    public async Task<ErrorOr<Success>> ExecuteAsync(
        IndexingJob indexingJob,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(indexingJob);
        cancellationToken.ThrowIfCancellationRequested();

        var rootDirectory = storageOptions.Value.RootDirectory;
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return Error.Failure(
                "BookStorage.RootDirectoryUnavailable",
                "The configured book storage directory does not exist.");
        }

        var filePaths = Directory
            .EnumerateFiles(rootDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(filePath => Path.GetExtension(filePath).Equals(
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var booksToIndex = new List<(Book Book, PdfTextExtractionResult Extraction)>(filePaths.Length);

        foreach (var filePath in filePaths)
        {
            var fileName = Path.GetFileName(filePath);
            string? fileError = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var book = await bookReadRepository.FindByFileName(fileName, cancellationToken);

                if (book is null)
                {
                    var extraction = await pdfTextExtractor.ExtractAsync(filePath, cancellationToken);
                    var createResult = await sender.Send(
                        new CreateBookCommand(fileName, extraction.Title, extraction.Author),
                        cancellationToken);
                    if (createResult.IsError)
                    {
                        fileError = string.Join(
                            "; ",
                            createResult.Errors.Select(error => error.Description));
                    }
                    else
                    {
                        book = createResult.Value;
                        booksToIndex.Add((book, extraction));
                    }
                }
                else if (book.IndexingStatus == BookIndexingStatus.Pending)
                {
                    var extraction = await pdfTextExtractor.ExtractAsync(filePath, cancellationToken);
                    booksToIndex.Add((book, extraction));
                }
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException ||
                !cancellationToken.IsCancellationRequested)
            {
                fileError = $"{exception.GetType().Name}: {exception.Message}";
            }

            if (fileError is not null)
            {
                var fileErrorResult = await sender.Send(
                    new AddIndexingJobFileErrorCommand(indexingJob.Id, fileName, fileError),
                    cancellationToken);
                if (fileErrorResult.IsError)
                {
                    return fileErrorResult.Errors;
                }
            }
        }

        var completedAttempts = 0;
        foreach (var (book, extraction) in booksToIndex)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? fileError = null;
            try
            {
                var startResult = await sender.Send(
                    new MarkBookIndexingStartedCommand(book.Id),
                    cancellationToken);
                if (startResult.IsError)
                {
                    fileError = string.Join(
                        "; ",
                        startResult.Errors.Select(error => error.Description));
                }
                else
                {
                    var usablePages = extraction.Pages
                        .Where(page => !string.IsNullOrWhiteSpace(page.Text))
                        .ToArray();
                    var chunks = new List<ReplaceDocumentChunkInput>(usablePages.Length);
                    var keywords = new List<string>();
                    for (var index = 0; index < usablePages.Length; index++)
                    {
                        var page = usablePages[index];
                        var embeddingTask = embeddingGenerator.GenerateAsync(
                            page.Text,
                            cancellationToken);
                        var keywordsTask = keywordExtractor.ExtractAsync(
                            page.Text,
                            cancellationToken);
                        await Task.WhenAll(embeddingTask, keywordsTask);
                        var embedding = await embeddingTask;
                        keywords.AddRange(await keywordsTask);
                        chunks.Add(new ReplaceDocumentChunkInput(
                            index,
                            page.PageNumber,
                            page.Text,
                            embedding));
                    }

                    var addKeywordsResult = await sender.Send(
                        new AddBookKeywordsCommand(book.Id, keywords),
                        cancellationToken);
                    if (addKeywordsResult.IsError)
                    {
                        fileError = AppendErrorMessage(
                            fileError,
                            string.Join(
                                "; ",
                                addKeywordsResult.Errors.Select(error => error.Description)));
                    }

                    var replaceChunksResult = await sender.Send(
                        new ReplaceDocumentChunksCommand(book.Id, chunks),
                        cancellationToken);
                    if (replaceChunksResult.IsError)
                    {
                        fileError = AppendErrorMessage(
                            fileError,
                            string.Join(
                                "; ",
                                replaceChunksResult.Errors.Select(error => error.Description)));
                    }

                    if (fileError is null)
                    {
                        var markIndexedResult = await sender.Send(
                            new MarkBookIndexedCommand(book.Id, DateTimeOffset.UtcNow),
                            cancellationToken);
                        if (markIndexedResult.IsError)
                        {
                            fileError = string.Join(
                                "; ",
                                markIndexedResult.Errors.Select(error => error.Description));
                        }
                    }
                }
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException ||
                !cancellationToken.IsCancellationRequested)
            {
                fileError = exception.Message;
            }

            if (fileError is not null)
            {
                var failBookResult = await sender.Send(
                    new MarkBookIndexingFailedCommand(book.Id),
                    cancellationToken);
                if (failBookResult.IsError)
                {
                    fileError = AppendErrorMessage(
                        fileError,
                        string.Join(
                            "; ",
                            failBookResult.Errors.Select(error => error.Description)));
                }

                var fileErrorResult = await sender.Send(
                    new AddIndexingJobFileErrorCommand(indexingJob.Id, book.SourceFileName, fileError),
                    cancellationToken);
                if (fileErrorResult.IsError)
                {
                    return fileErrorResult.Errors;
                }
            }

            completedAttempts++;
            await sender.Send(
                new UpdateIndexingJobProgressCommand(
                    indexingJob.Id,
                    $"{completedAttempts} of {booksToIndex.Count} books indexed"),
                cancellationToken);
        }

        return Result.Success;
    }

    private static string AppendErrorMessage(string? currentMessage, string additionalMessage) =>
        string.IsNullOrWhiteSpace(currentMessage)
            ? additionalMessage
            : $"{currentMessage}; {additionalMessage}";
}
