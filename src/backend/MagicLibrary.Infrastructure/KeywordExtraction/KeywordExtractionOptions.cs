namespace MagicLibrary.Infrastructure.KeywordExtraction;

public sealed class KeywordExtractionOptions
{
    public const string SectionName = "KeywordExtraction";

    public int MaxResults { get; set; } = 3;
}
