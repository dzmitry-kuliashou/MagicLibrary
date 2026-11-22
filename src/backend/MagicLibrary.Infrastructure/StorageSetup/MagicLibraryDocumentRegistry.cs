using Marten;
using Marten.Schema;
using MagicLibrary.Infrastructure.StorageModels.Documents;
using Weasel.Postgresql.Tables;

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
            .Duplicate(
                document => document.Title,
                configure: index =>
                {
                    index.Name = "idx_book_title_trgm";
                    index.Method = IndexMethod.gin;
                    index.Mask = "? gin_trgm_ops";
                })
            .Duplicate(
                document => document.Author,
                configure: index =>
                {
                    index.Name = "idx_book_author_trgm";
                    index.Method = IndexMethod.gin;
                    index.Mask = "? gin_trgm_ops";
                })
            .GinIndexJsonDataMember(
                document => document.Keywords,
                index => index.Name = "idx_book_keywords_gin");
        For<IndexingJobDocument>();
        For<ConversationDocument>();
    }
}
