using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.KeywordExtraction;
using MagicLibrary.Application.PdfProcessing;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;
using Microsoft.Extensions.Options;

namespace MagicLibrary.Application.Indexing;

public sealed class BookIndexingScenario(
    IOptions<BookStorageOptions> storageOptions,
    IPdfTextExtractor pdfTextExtractor,
    IBookReadRepository bookReadRepository,
    IEmbeddingGenerator embeddingGenerator,
    IKeywordExtractor keywordExtractor,
    ISender sender) : IIndexingScenario
{
    public bool CanProcess(IndexingJob indexingJob) => indexingJob.BookId is not null;

    public async Task<ErrorOr<Success>> ExecuteAsync(
        IndexingJob indexingJob,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(indexingJob);
        cancellationToken.ThrowIfCancellationRequested();

        if (indexingJob.BookId is not { } bookId)
        {
            return IndexingJobErrors.BookIdRequired;
        }

        var book = await bookReadRepository.GetByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            return BookErrors.BookNotFound(bookId);
        }

        var rootDirectory = storageOptions.Value.RootDirectory;
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return IndexingJobErrors.BookStorageRootDirectoryUnavailable;
        }

        var filePath = Path.Combine(rootDirectory, book.SourceFileName);
        if (!File.Exists(filePath))
        {
            return IndexingJobErrors.BookPdfNotFound(book.SourceFileName);
        }

        PdfTextExtractionResult extraction;
        try
        {
            extraction = await pdfTextExtractor.ExtractAsync(filePath, cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            return IndexingJobErrors.BookPdfExtractionFailed(book.SourceFileName, exception);
        }

        var resetResult = await sender.Send(
            new ResetBookIndexingCommand(bookId),
            cancellationToken);
        if (resetResult.IsError)
        {
            return resetResult.Errors;
        }

        var startResult = await sender.Send(
            new MarkBookIndexingStartedCommand(bookId),
            cancellationToken);
        if (startResult.IsError)
        {
            return startResult.Errors;
        }

        string? fileError = null;
        try
        {
            var usablePages = extraction.Pages
                .Where(page => !string.IsNullOrWhiteSpace(page.Text))
                .ToArray();
            var chunks = new List<ReplaceDocumentChunkInput>(usablePages.Length);
            var keywords = new List<string>();
            for (var index = 0; index < usablePages.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = usablePages[index];
                var embeddingTask = embeddingGenerator.GenerateAsync(page.Text, cancellationToken);
                var keywordsTask = ExtractKeywordsAsync(page.Text, cancellationToken);
                await Task.WhenAll(embeddingTask, keywordsTask);
                var embedding = await embeddingTask;
                keywords.AddRange(await keywordsTask);
                chunks.Add(new ReplaceDocumentChunkInput(
                    index,
                    page.PageNumber,
                    page.Text,
                    embedding));

                await sender.Send(
                    new UpdateIndexingJobProgressCommand(
                        indexingJob.Id,
                        $"{index + 1} of {usablePages.Length} pages indexed"),
                    cancellationToken);
            }

            var addKeywordsResult = await sender.Send(
                new AddBookKeywordsCommand(bookId, keywords),
                cancellationToken);
            if (addKeywordsResult.IsError)
            {
                fileError = string.Join(
                    "; ",
                    addKeywordsResult.Errors.Select(error => error.Description));
            }

            var replaceChunksResult = await sender.Send(
                new ReplaceDocumentChunksCommand(bookId, chunks),
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
                    new MarkBookIndexedCommand(bookId, DateTimeOffset.UtcNow),
                    cancellationToken);
                if (markIndexedResult.IsError)
                {
                    fileError = string.Join(
                        "; ",
                        markIndexedResult.Errors.Select(error => error.Description));
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
                new MarkBookIndexingFailedCommand(bookId),
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
                new AddIndexingJobFileErrorCommand(
                    indexingJob.Id,
                    book.SourceFileName,
                    fileError),
                cancellationToken);
            if (fileErrorResult.IsError)
            {
                return fileErrorResult.Errors;
            }

            return IndexingJobErrors.BookIndexingFailed(book.SourceFileName, fileError);
        }

        return Result.Success;
    }

    private async Task<IReadOnlyList<string>> ExtractKeywordsAsync(
        string pageText,
        CancellationToken cancellationToken)
    {
        try
        {
            return await keywordExtractor.ExtractAsync(pageText, cancellationToken);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }
    }

    private static string AppendErrorMessage(string? currentMessage, string additionalMessage) =>
        string.IsNullOrWhiteSpace(currentMessage)
            ? additionalMessage
            : $"{currentMessage}; {additionalMessage}";
}
