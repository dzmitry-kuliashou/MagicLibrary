using ErrorOr;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.Responses;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Infrastructure.Chat;
using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Chat;

public sealed class LibraryChatSearchTests
{
    private readonly Mock<ISender> _sender = new();
    private readonly Mock<IEmbeddingGenerator> _embeddingGenerator = new();
    private readonly Mock<IBookSearchTermExtractor> _bookSearchTermExtractor = new();

    [Fact]
    public async Task SearchBooksAsync_ExtractedTerms_ForwardsQueryAndCancellationToken()
    {
        const string latestUserMessage = "Find The Hobbit by Tolkien.";
        var terms = new BookSearchTerms(
            ["The Hobbit"],
            ["Tolkien"],
            ["fantasy adventure"]);
        IReadOnlyList<Guid> expectedBookIds = [Guid.NewGuid(), Guid.NewGuid()];
        var response = new SearchBooksResponse(expectedBookIds);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _bookSearchTermExtractor
            .Setup(extractor => extractor.ExtractAsync(latestUserMessage, cancellationToken))
            .ReturnsAsync(terms);
        _sender
            .Setup(sender => sender.Send(
                It.Is<SearchBooksQuery>(query =>
                    query.Terms != null &&
                    query.Terms.SequenceEqual(new[] { "The Hobbit", "Tolkien", "fantasy adventure" })),
                cancellationToken))
            .ReturnsAsync(response);
        var search = CreateSearch();

        var result = await search.SearchBooksAsync(latestUserMessage, cancellationToken);

        Assert.Same(expectedBookIds, result);
        _sender.Verify(
            sender => sender.Send(
                It.Is<SearchBooksQuery>(query =>
                    query.Terms != null &&
                    query.Terms.SequenceEqual(new[] { "The Hobbit", "Tolkien", "fantasy adventure" })),
                cancellationToken),
            Times.Once);
        _bookSearchTermExtractor.Verify(
            extractor => extractor.ExtractAsync(latestUserMessage, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task SearchBooksAsync_NoBookSearchTerms_SkipsQueryDispatch()
    {
        const string latestUserMessage = "Recommend a book.";
        _bookSearchTermExtractor
            .Setup(extractor => extractor.ExtractAsync(latestUserMessage, CancellationToken.None))
            .ReturnsAsync(BookSearchTerms.Empty);
        var search = CreateSearch();

        var result = await search.SearchBooksAsync(latestUserMessage, CancellationToken.None);

        Assert.Empty(result);
        _sender.Verify(
            sender => sender.Send(
                It.IsAny<SearchBooksQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchChunksAsync_RetrievedChunks_ReturnsResolvedSourceReferences()
    {
        const string query = "Where does Bilbo find the ring?";
        const int topK = 7;
        var book = Book.Create(
            "the-hobbit.pdf",
            "The Hobbit",
            "J. R. R. Tolkien",
            "content-hash",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var bookIds = new[] { book.Id };
        var embedding = new[] { 0.1f, 0.2f, 0.3f };
        var chunk = DocumentChunk.Create(
            book.Id,
            4,
            12,
            "Bilbo finds the ring in the cave.",
            embedding,
            "test-provider",
            "test-model");
        IReadOnlyList<DocumentChunk> chunks = [chunk];
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _embeddingGenerator
            .Setup(generator => generator.GenerateAsync(query, cancellationToken))
            .ReturnsAsync(embedding);
        _sender
            .Setup(sender => sender.Send(
                It.Is<SearchDocumentChunksQuery>(request =>
                    request.QueryEmbedding.SequenceEqual(embedding) &&
                    request.BookIds != null &&
                    request.BookIds.SequenceEqual(bookIds) &&
                    request.TopK == topK),
                cancellationToken))
            .ReturnsAsync(chunks);
        _sender
            .Setup(sender => sender.Send(
                It.Is<GetBookByIdQuery>(request => request.Id == book.Id),
                cancellationToken))
            .ReturnsAsync((ErrorOr<Book>)book);
        var tools = CreateSearch(topK);

        var result = await tools.SearchChunksAsync(query, bookIds, cancellationToken);

        var retrievedChunk = Assert.Single(result);
        Assert.Equal(chunk.Content, retrievedChunk.Content);
        Assert.NotNull(retrievedChunk.SourceReference);
        Assert.Equal(book.Id, retrievedChunk.SourceReference.BookId);
        Assert.Equal(book.Title, retrievedChunk.SourceReference.BookTitle);
        Assert.Equal(chunk.PageNumber, retrievedChunk.SourceReference.PageNumber);
        Assert.Equal(chunk.Id, retrievedChunk.SourceReference.ChunkId);
        _embeddingGenerator.Verify(
            generator => generator.GenerateAsync(query, cancellationToken),
            Times.Once);
        _sender.Verify(
            sender => sender.Send(
                It.Is<SearchDocumentChunksQuery>(request =>
                    request.QueryEmbedding.SequenceEqual(embedding) &&
                    request.BookIds != null &&
                    request.BookIds.SequenceEqual(bookIds) &&
                    request.TopK == topK),
                cancellationToken),
            Times.Once);
        _sender.Verify(
            sender => sender.Send(
                It.Is<GetBookByIdQuery>(request => request.Id == book.Id),
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task SearchChunksAsync_MissingBookTitle_KeepsChunkWithoutSourceReference()
    {
        const string query = "Where does Bilbo find the ring?";
        var bookId = Guid.NewGuid();
        var embedding = new[] { 0.1f, 0.2f, 0.3f };
        var chunk = DocumentChunk.Create(
            bookId,
            4,
            12,
            "Bilbo finds the ring in the cave.",
            embedding,
            "test-provider",
            "test-model");
        IReadOnlyList<DocumentChunk> chunks = [chunk];
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _embeddingGenerator
            .Setup(generator => generator.GenerateAsync(query, cancellationToken))
            .ReturnsAsync(embedding);
        _sender
            .Setup(sender => sender.Send(
                It.IsAny<SearchDocumentChunksQuery>(),
                cancellationToken))
            .ReturnsAsync(chunks);
        _sender
            .Setup(sender => sender.Send(
                It.Is<GetBookByIdQuery>(request => request.Id == bookId),
                cancellationToken))
            .ReturnsAsync((ErrorOr<Book>)BookErrors.BookNotFound(bookId));
        var tools = CreateSearch();

        var result = await tools.SearchChunksAsync(query, [bookId], cancellationToken);

        var retrievedChunk = Assert.Single(result);
        Assert.Equal(chunk.Content, retrievedChunk.Content);
        Assert.Null(retrievedChunk.SourceReference);
        _sender.Verify(
            sender => sender.Send(
                It.Is<GetBookByIdQuery>(request => request.Id == bookId),
                cancellationToken),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SearchChunksAsync_UndefinedQuery_ReturnsEmptyWithoutEmbedding(string? query)
    {
        var tools = CreateSearch();

        var result = await tools.SearchChunksAsync(query!, null, CancellationToken.None);

        Assert.Empty(result);
        _sender.VerifyNoOtherCalls();
        _embeddingGenerator.VerifyNoOtherCalls();
    }

    private LibraryChatSearch CreateSearch(int topK = 5)
    {
        return new LibraryChatSearch(
            _sender.Object,
            _embeddingGenerator.Object,
            Options.Create(new VectorSearchOptions { TopK = topK }),
            _bookSearchTermExtractor.Object);
    }
}
