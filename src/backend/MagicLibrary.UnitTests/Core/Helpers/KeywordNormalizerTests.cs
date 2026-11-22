using MagicLibrary.Core.Helpers;
using Xunit;

namespace MagicLibrary.UnitTests.Core.Helpers;

public sealed class KeywordNormalizerTests
{
    [Fact]
    public void Normalize_MixedSeparators_ReturnsCanonicalKeyword()
    {
        var normalized = KeywordNormalizer.Normalize("  --This Is...Key__Word!!  ");

        Assert.Equal("this_is_key_word", normalized);
    }
}
