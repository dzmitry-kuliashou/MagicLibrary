namespace MagicLibrary.Contract.IndexingJobs.Requests;

public sealed record RunIndexingRequest(Guid? BookId = null);
