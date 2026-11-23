namespace MagicLibrary.Application.KeywordExtraction;

public interface IKeywordExtractor
{
    Task<IReadOnlyList<string>> ExtractAsync(
        string text,
        CancellationToken cancellationToken);
}
