using MagicLibrary.Infrastructure.PdfProcessing;
using Xunit;

namespace MagicLibrary.UnitTests.PdfProcessing;

public sealed class PdfPigTextExtractorTests
{
    [Fact]
    public async Task ExtractAsync_PreservesPageNumbersAndBoundaries()
    {
        var extractor = new PdfPigTextExtractor();

        var result = await extractor.ExtractAsync(
            GetFixturePath("multi-page.pdf"),
            CancellationToken.None);

        Assert.Collection(
            result.Pages,
            page =>
            {
                Assert.Equal(1, page.PageNumber);
                Assert.Contains("First page body", page.Text);
                Assert.DoesNotContain("Second page body", page.Text);
            },
            page =>
            {
                Assert.Equal(2, page.PageNumber);
                Assert.Contains("Second page body", page.Text);
                Assert.DoesNotContain("First page body", page.Text);
            });
    }

    [Fact]
    public async Task ExtractAsync_WithMetadata_ReturnsTitleAndAuthor()
    {
        var extractor = new PdfPigTextExtractor();

        var result = await extractor.ExtractAsync(
            GetFixturePath("metadata.pdf"),
            CancellationToken.None);

        Assert.Equal("The Metadata Book", result.Title);
        Assert.Equal("Sample Author", result.Author);
    }

    [Fact]
    public async Task ExtractAsync_WithoutTitle_UsesSourceFilenameAndLeavesAuthorEmpty()
    {
        var extractor = new PdfPigTextExtractor();

        var result = await extractor.ExtractAsync(
            GetFixturePath("missing-title.pdf"),
            CancellationToken.None);

        Assert.Equal("missing-title.pdf", result.Title);
        Assert.Null(result.Author);
    }

    [Fact]
    public async Task ExtractAsync_WithInvalidPdf_ThrowsInsteadOfReturningEmptyResult()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(filePath, "not a PDF");

        try
        {
            var extractor = new PdfPigTextExtractor();

            await Assert.ThrowsAnyAsync<Exception>(() =>
                extractor.ExtractAsync(filePath, CancellationToken.None));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string GetFixturePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "PdfProcessing", "Fixtures", fileName);
    }
}
