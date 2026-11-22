namespace MagicLibrary.Application.PdfProcessing;

public sealed record PdfTextExtractionResult(
    IReadOnlyList<PdfTextExtractionPage> Pages,
    string Title,
    string? Author = null);

public sealed record PdfTextExtractionPage(int PageNumber, string Text);
