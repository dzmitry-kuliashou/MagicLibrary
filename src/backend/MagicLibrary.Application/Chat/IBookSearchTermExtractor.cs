namespace MagicLibrary.Application.Chat;

public interface IBookSearchTermExtractor
{
    Task<BookSearchTerms> ExtractAsync(
        string latestUserMessage,
        CancellationToken cancellationToken);
}

public sealed record BookSearchTerms(
    IReadOnlyList<string> ExactTitleTerms,
    IReadOnlyList<string> ExactAuthorTerms,
    IReadOnlyList<string> KeywordPhrases)
{
    public static BookSearchTerms Empty { get; } = new([], [], []);

    public bool HasSearchTerms =>
        ExactTitleTerms.Count > 0 ||
        ExactAuthorTerms.Count > 0 ||
        KeywordPhrases.Count > 0;
}
