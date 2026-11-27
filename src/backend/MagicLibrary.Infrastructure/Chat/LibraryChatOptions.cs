using Microsoft.Extensions.AI;

namespace MagicLibrary.Infrastructure.Chat;

public sealed class LibraryChatOptions
{
    public const string SectionName = "LibraryChat";

    public LibraryChatStageOptions ContextSelection { get; set; } = new();

    public LibraryChatStageOptions SearchTermExtraction { get; set; } = new();

    public LibraryChatStageOptions AnswerGeneration { get; set; } = new();
}

public sealed class LibraryChatStageOptions
{
    public int MaxOutputTokens { get; set; }

    public ReasoningEffort ReasoningEffort { get; set; } = ReasoningEffort.None;
}
