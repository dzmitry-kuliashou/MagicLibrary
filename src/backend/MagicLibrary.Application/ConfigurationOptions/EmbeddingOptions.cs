namespace MagicLibrary.Application.ConfigurationOptions;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public string Provider { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int Dimensions { get; set; }
}
