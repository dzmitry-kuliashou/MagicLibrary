using MagicLibrary.Core.ValueObjects;
using Xunit;

namespace MagicLibrary.UnitTests.Core.ValueObjects;

public sealed class SourceReferenceTests
{
    [Fact]
    public void Create_ValidAttribution_RetainsBookPageAndOptionalChunk()
    {
        var bookId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var chunkId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var reference = SourceReference.Create(bookId, "Book title", 4, chunkId);

        Assert.Equal(bookId, reference.BookId);
        Assert.Equal("Book title", reference.BookTitle);
        Assert.Equal(4, reference.PageNumber);
        Assert.Equal(chunkId, reference.ChunkId);
    }

    [Fact]
    public void Create_WithoutChunkId_RetainsNullChunkId()
    {
        var reference = SourceReference.Create(Guid.NewGuid(), "Book title", 1);

        Assert.Null(reference.ChunkId);
    }

    [Fact]
    public void Create_DefaultBookId_RejectsValue()
    {
        Assert.ThrowsAny<ArgumentException>(() => SourceReference.Create(
            Guid.Empty,
            "Book title",
            1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankBookTitle_RejectsValue(string? bookTitle)
    {
        Assert.ThrowsAny<ArgumentException>(() => SourceReference.Create(
            Guid.NewGuid(),
            bookTitle!,
            1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositivePageNumber_RejectsValue(int pageNumber)
    {
        Assert.ThrowsAny<ArgumentException>(() => SourceReference.Create(
            Guid.NewGuid(),
            "Book title",
            pageNumber));
    }

    [Fact]
    public void Create_EmptyChunkId_RejectsValue()
    {
        Assert.ThrowsAny<ArgumentException>(() => SourceReference.Create(
            Guid.NewGuid(),
            "Book title",
            1,
            Guid.Empty));
    }
}
