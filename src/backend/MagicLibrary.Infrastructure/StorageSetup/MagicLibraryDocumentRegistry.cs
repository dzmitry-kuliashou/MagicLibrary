using Marten;
using Marten.Schema;
using MagicLibrary.Infrastructure.StorageModels.Documents;

namespace MagicLibrary.Infrastructure.StorageSetup;

public sealed class MagicLibraryDocumentRegistry : MartenRegistry
{
    public MagicLibraryDocumentRegistry()
    {
        For<BookDocument>()
            .Index(
                document => document.SourceFileName,
                index =>
                {
                    index.Name = "idx_book_source_file_name_ci_unique";
                    index.Casing = ComputedIndex.Casings.Lower;
                    index.IsUnique = true;
                })
            .GinIndexJsonDataMember(
                document => document.Keywords,
                index => index.Name = "idx_book_keywords_gin");
        For<IndexingJobDocument>();
        For<ConversationDocument>();
    }
}
