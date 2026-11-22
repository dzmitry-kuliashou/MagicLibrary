using MagicLibrary.Application.PdfProcessing;
using UglyToad.PdfPig;

namespace MagicLibrary.Infrastructure.PdfProcessing;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public async Task<PdfTextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var pdfBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var document = PdfDocument.Open(pdfBytes);
            var pdfTitle = document.Information.Title;
            var title = string.IsNullOrWhiteSpace(pdfTitle)
                ? Path.GetFileName(filePath) ?? throw new ArgumentException(
                    "The PDF file path must include a filename.",
                    nameof(filePath))
                : pdfTitle;
            var pdfAuthor = document.Information.Author;
            var author = string.IsNullOrWhiteSpace(pdfAuthor) ? null : pdfAuthor;
            var pages = new List<PdfTextExtractionPage>();

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages.Add(new PdfTextExtractionPage(page.Number, page.Text));
            }

            return new PdfTextExtractionResult(pages, title, author);
        }, cancellationToken);
    }
}
