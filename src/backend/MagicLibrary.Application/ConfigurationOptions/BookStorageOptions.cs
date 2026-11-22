namespace MagicLibrary.Application.ConfigurationOptions;

public sealed class BookStorageOptions
{
    public const string SectionName = "BookStorage";

    public string RootDirectory { get; set; } = string.Empty;
}
