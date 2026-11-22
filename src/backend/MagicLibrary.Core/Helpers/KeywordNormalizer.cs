using System.Text.RegularExpressions;

namespace MagicLibrary.Core.Helpers;

public static partial class KeywordNormalizer
{
    public static string Normalize(string keyword)
    {
        ArgumentNullException.ThrowIfNull(keyword);

        var normalized = SeparatorsRegex().Replace(keyword.Trim().ToLowerInvariant(), "_");
        return normalized.Trim('_');
    }

    [GeneratedRegex(@"[\s\p{P}]+")]
    private static partial Regex SeparatorsRegex();
}
