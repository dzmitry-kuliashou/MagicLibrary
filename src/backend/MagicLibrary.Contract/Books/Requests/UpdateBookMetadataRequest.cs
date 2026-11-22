namespace MagicLibrary.Contract.Books.Requests;

public sealed record UpdateBookMetadataRequest(string Title, string? Author);
