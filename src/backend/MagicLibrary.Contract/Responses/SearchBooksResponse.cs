namespace MagicLibrary.Contract.Responses;

public sealed record SearchBooksResponse(IReadOnlyList<Guid> BookIds);
