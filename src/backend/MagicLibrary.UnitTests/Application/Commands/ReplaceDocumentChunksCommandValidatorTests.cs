using MagicLibrary.Application.Commands;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validators;
using Microsoft.Extensions.Options;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class ReplaceDocumentChunksCommandValidatorTests
{
    private const int EmbeddingDimensions = 3;

    [Fact]
    public async Task Validate_ValidCommand_IsValid()
    {
        var result = await CreateValidator().ValidateAsync(CreateCommand());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("book-id")]
    [InlineData("chunk-index")]
    [InlineData("page-number")]
    [InlineData("content")]
    [InlineData("null-embedding")]
    [InlineData("empty-embedding")]
    public async Task Validate_DomainInvalidValues_ReturnsValidationError(string invalidField)
    {
        var (command, expectedErrorCode) = invalidField switch
        {
            "book-id" => (CreateCommand(bookId: Guid.Empty), DocumentChunkErrors.InvalidBookId.Code),
            "chunk-index" => (
                CreateCommand(chunks: [CreateChunk() with { ChunkIndex = -1 }]),
                DocumentChunkErrors.InvalidChunkIndex.Code),
            "page-number" => (
                CreateCommand(chunks: [CreateChunk() with { PageNumber = 0 }]),
                DocumentChunkErrors.InvalidPageNumber.Code),
            "content" => (
                CreateCommand(chunks: [CreateChunk() with { Content = " " }]),
                DocumentChunkErrors.MissingContent.Code),
            "null-embedding" => (
                CreateCommand(chunks: [CreateChunk() with { Embedding = null! }]),
                DocumentChunkErrors.MissingEmbedding.Code),
            "empty-embedding" => (
                CreateCommand(chunks: [CreateChunk() with { Embedding = [] }]),
                DocumentChunkErrors.MissingEmbedding.Code),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField))
        };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.ErrorCode == expectedErrorCode);
    }

    [Fact]
    public async Task Validate_EmptyChunks_ReturnsValidationError()
    {
        var result = await CreateValidator().ValidateAsync(
            CreateCommand(chunks: Array.Empty<ReplaceDocumentChunkInput>()));

        var failure = Assert.Single(result.Errors);
        Assert.False(result.IsValid);
        Assert.Equal(DocumentChunkErrors.MissingChunks.Code, failure.ErrorCode);
    }

    [Fact]
    public async Task Validate_EmbeddingDimensionMismatch_ReturnsValidationError()
    {
        var result = await CreateValidator().ValidateAsync(
            CreateCommand(chunks: [CreateChunk() with { Embedding = [0.1f, 0.2f] }]));

        var failure = Assert.Single(result.Errors);
        Assert.False(result.IsValid);
        Assert.Equal(DocumentChunkErrors.InvalidEmbeddingDimensions(EmbeddingDimensions).Code, failure.ErrorCode);
        Assert.Equal(
            DocumentChunkErrors.InvalidEmbeddingDimensions(EmbeddingDimensions).Description,
            failure.ErrorMessage);
    }

    private static ReplaceDocumentChunksCommandValidator CreateValidator()
    {
        return new ReplaceDocumentChunksCommandValidator(
            Options.Create(new EmbeddingOptions
            {
                Provider = "Ollama",
                BaseUrl = "http://localhost:11435",
                Model = "nomic-embed-text:latest",
                Dimensions = EmbeddingDimensions
            }));
    }

    private static ReplaceDocumentChunksCommand CreateCommand(
        Guid? bookId = null,
        IReadOnlyList<ReplaceDocumentChunkInput>? chunks = null)
    {
        return new ReplaceDocumentChunksCommand(
            bookId ?? Guid.NewGuid(),
            chunks ?? [CreateChunk()]);
    }

    private static ReplaceDocumentChunkInput CreateChunk()
    {
        return new ReplaceDocumentChunkInput(
            0,
            1,
            "A valid document chunk.",
            [0.1f, 0.2f, 0.3f]);
    }
}
