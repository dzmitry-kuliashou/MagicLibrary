namespace MagicLibrary.Contract.Requests;

public sealed record SearchBooksRequest(IReadOnlyList<string> Terms);
