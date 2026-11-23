using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validators;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Vectors;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Commands;

public sealed class ReplaceDocumentChunksCommandHandlerTests
{
    private const int EmbeddingDimensions = 3;

    [Fact]
    public async Task Handle_ValidCommand_ReplacesChunksWithConfiguredProvenance()
    {
        var (handler, repository) = CreateHandler();
        var command = CreateCommand();
        using var cancellationSource = new CancellationTokenSource();
        Guid? replacedBookId = null;
        IReadOnlyCollection<DocumentChunk>? replacedChunks = null;
        CancellationToken? receivedCancellationToken = null;
        repository
            .Setup(writeRepository => writeRepository.ReplaceAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<DocumentChunk>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<DocumentChunk>, CancellationToken>(
                (bookId, chunks, cancellationToken) =>
                {
                    replacedBookId = bookId;
                    replacedChunks = chunks;
                    receivedCancellationToken = cancellationToken;
                })
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(command, cancellationSource.Token);

        Assert.False(result.IsError);
        Assert.Equal(command.BookId, replacedBookId);
        Assert.Equal(cancellationSource.Token, receivedCancellationToken);

        var chunk = Assert.Single(replacedChunks!);
        Assert.Equal(command.BookId, chunk.BookId);
        Assert.Equal(0, chunk.ChunkIndex);
        Assert.Equal(1, chunk.PageNumber);
        Assert.Equal("A valid document chunk.", chunk.Content);
        Assert.Equal([0.1f, 0.2f, 0.3f], chunk.Embedding);
        Assert.Equal("Ollama", chunk.EmbeddingProvider);
        Assert.Equal("nomic-embed-text:latest", chunk.EmbeddingModel);
        repository.Verify(
            writeRepository => writeRepository.ReplaceAsync(
                command.BookId,
                It.IsAny<IReadOnlyCollection<DocumentChunk>>(),
                cancellationSource.Token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidCommand_ReturnsValidationErrorsWithoutRepositoryAccess()
    {
        var (handler, repository) = CreateHandler();
        var command = CreateCommand() with { Chunks = [] };

        var result = await handler.Handle(command, CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(DocumentChunkErrors.MissingChunks.Code, error.Code);
        repository.Verify(
            writeRepository => writeRepository.ReplaceAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<DocumentChunk>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static (
        ReplaceDocumentChunksCommandHandler Handler,
        Mock<IDocumentChunkWriteRepository> Repository) CreateHandler()
    {
        var options = CreateOptions();
        var repository = new Mock<IDocumentChunkWriteRepository>();
        var validator = new ReplaceDocumentChunksCommandValidator(Options.Create(options));
        var handler = new ReplaceDocumentChunksCommandHandler(
            validator,
            Options.Create(options),
            repository.Object);

        return (handler, repository);
    }

    private static EmbeddingOptions CreateOptions()
    {
        return new EmbeddingOptions
        {
            Provider = "Ollama",
            BaseUrl = "http://localhost:11435",
            Model = "nomic-embed-text:latest",
            Dimensions = EmbeddingDimensions
        };
    }

    private static ReplaceDocumentChunksCommand CreateCommand()
    {
        return new ReplaceDocumentChunksCommand(
            Guid.NewGuid(),
            [
                new ReplaceDocumentChunkInput(
                    0,
                    1,
                    "A valid document chunk.",
                    [0.1f, 0.2f, 0.3f])
            ]);
    }
}
