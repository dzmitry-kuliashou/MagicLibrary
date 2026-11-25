namespace MagicLibrary.Contract.Responses;

public sealed record BookResponse(Guid Id, string Title, string Author, string IndexingStatus);
