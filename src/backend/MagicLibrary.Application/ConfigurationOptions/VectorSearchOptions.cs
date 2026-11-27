namespace MagicLibrary.Application.ConfigurationOptions;

public sealed class VectorSearchOptions
{
    public const string SectionName = "VectorSearch";

    public int TopK { get; set; } = 5;
}
