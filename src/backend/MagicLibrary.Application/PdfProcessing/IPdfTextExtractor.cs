namespace MagicLibrary.Application.PdfProcessing;

public interface IPdfTextExtractor
{
    Task<PdfTextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken);
}
