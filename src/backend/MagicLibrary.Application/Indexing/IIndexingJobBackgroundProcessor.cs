namespace MagicLibrary.Application.Indexing;

public interface IIndexingJobBackgroundProcessor
{
    void Start(Guid indexingJobId);
}
