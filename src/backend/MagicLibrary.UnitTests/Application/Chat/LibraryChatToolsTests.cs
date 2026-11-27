using ErrorOr;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Queries;
using MagicLibrary.Contract.Responses;
using MagicLibrary.Core.Aggregates;
using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Chat;

public sealed class LibraryChatToolsTests
{
    private readonly Mock<ISender> _sender = new();
    private readonly Mock<IEmbeddingGenerator> _embeddingGenerator = new();

    [Fact]
    public async Task SearchBooksAsync_ValidTerms_ForwardsQueryAndCancellationToken()
    {
        IReadOnlyList<string> terms = ["The Hobbit", "Tolkien"];
        IReadOnlyList<Guid> expectedBookIds = [Guid.NewGuid(), Guid.NewGuid()];
        var response = new SearchBooksResponse(expectedBookIds);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _sender
            .Setup(sender => sender.Send(
                It.Is<SearchBooksQuery>(query => ReferenceEquals(query.Terms, terms)),
                cancellationToken))
            .ReturnsAsync(response);
        var tools = CreateTools();

        var result = await tools.SearchBooksAsync(terms, cancellationToken);

        Assert.Same(expectedBookIds, result);
        _sender.Verify(
            sender => sender.Send(
                It.Is<SearchBooksQuery>(query => ReferenceEquals(query.Terms, terms)),
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task SearchBooksAsync_NullTerms_ForwardsToQueryHandler()
    {
        IReadOnlyList<Guid> expectedBookIds = Array.Empty<Guid>();
        var response = new SearchBooksResponse(expectedBookIds);
        _sender
            .Setup(sender => sender.Send(
                It.Is<SearchBooksQuery>(query => query.Terms == null),
                CancellationToken.None))
            .ReturnsAsync(response);
        var tools = CreateTools();

        var result = await tools.SearchBooksAsync(null!, CancellationToken.None);

        Assert.Same(expectedBookIds, result);
        _sender.Verify(
            sender => sender.Send(
                It.Is<SearchBooksQuery>(query => query.Terms == null),
                CancellationToken.None),
            Times.Once);
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
        var tools = CreateTools(topK);

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
        var tools = CreateTools();

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
        var tools = CreateTools();

        var result = await tools.SearchChunksAsync(query!, null, CancellationToken.None);

        Assert.Empty(result);
        _sender.VerifyNoOtherCalls();
        _embeddingGenerator.VerifyNoOtherCalls();
    }

    private LibraryChatTools CreateTools(int topK = 5)
    {
        return new LibraryChatTools(
            _sender.Object,
            _embeddingGenerator.Object,
            Options.Create(new VectorSearchOptions { TopK = topK }));
    }
}
