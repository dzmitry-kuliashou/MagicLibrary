namespace MagicLibrary.Infrastructure.KeywordExtraction;

public sealed class KeywordExtractionOptions
{
    public const string SectionName = "KeywordExtraction";
    public const int MaximumPhraseWords = 3;

    public int MaxResults { get; set; } = 3;

    public int MaxGeneratedTokens { get; set; } = 128;
}
