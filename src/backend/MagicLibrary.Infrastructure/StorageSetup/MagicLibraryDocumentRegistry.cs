using Marten;
using MagicLibrary.Infrastructure.StorageModels;

namespace MagicLibrary.Infrastructure.StorageSetup;

public sealed class MagicLibraryDocumentRegistry : MartenRegistry
{
    public MagicLibraryDocumentRegistry()
    {
        For<BookDocument>();
        For<IndexingJobDocument>();
        For<ConversationDocument>();
    }
}
