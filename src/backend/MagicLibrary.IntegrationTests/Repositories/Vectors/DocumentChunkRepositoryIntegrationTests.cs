using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Vectors;
using MagicLibrary.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace MagicLibrary.IntegrationTests.Repositories.Vectors;

[Collection(IntegrationTestCollection.Name)]
public sealed class DocumentChunkRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;
    private readonly IDocumentChunkReadRepository _readRepository;
    private readonly IDocumentChunkWriteRepository _writeRepository;

    public DocumentChunkRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _serviceProvider = fixture.CreateServiceProvider();
        _scope = _serviceProvider.CreateAsyncScope();
        _readRepository = _scope.ServiceProvider.GetRequiredService<IDocumentChunkReadRepository>();
        _writeRepository = _scope.ServiceProvider.GetRequiredService<IDocumentChunkWriteRepository>();
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("TRUNCATE TABLE document_chunks", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task ReplaceAsync_ExistingChunks_ReplacesStoredChunksForBook()
    {
        var bookId = Guid.NewGuid();
        var existingChunks = new[]
        {
            CreateChunk(bookId, 0, [1f, 0f, 0f], "previous chunk")
        };
        var replacementChunks = new[]
        {
            CreateChunk(bookId, 0, [0f, 1f, 0f], "replacement one"),
            CreateChunk(bookId, 1, [0f, 0f, 1f], "replacement two")
        };

        await _writeRepository.ReplaceAsync(bookId, existingChunks, CancellationToken.None);
        await _writeRepository.ReplaceAsync(bookId, replacementChunks, CancellationToken.None);

        var results = await _readRepository.SearchAsync(
            [0f, 1f, 0f],
            10,
            [bookId],
            CancellationToken.None);
        var expectedContents = replacementChunks
            .Select(chunk => chunk.Content)
            .OrderBy(content => content, StringComparer.Ordinal);
        var actualContents = results
            .Select(chunk => chunk.Content)
            .OrderBy(content => content, StringComparer.Ordinal);

        Assert.Equal(expectedContents, actualContents);
        Assert.DoesNotContain(results, chunk => chunk.Content == "previous chunk");
    }

    [Fact]
    public async Task SearchAsync_UnscopedQuery_ReturnsNearestChunksInOrderUpToTopK()
    {
        var bookId = Guid.NewGuid();
        var chunks = new[]
        {
            CreateChunk(bookId, 0, [0f, 1f, 0f], "far chunk"),
            CreateChunk(bookId, 1, [0.8f, 0.6f, 0f], "second nearest chunk"),
            CreateChunk(bookId, 2, [-1f, 0f, 0f], "farthest chunk"),
            CreateChunk(bookId, 3, [1f, 0f, 0f], "nearest chunk")
        };
        await _writeRepository.ReplaceAsync(bookId, chunks, CancellationToken.None);

        var results = await _readRepository.SearchAsync(
            [1f, 0f, 0f],
            2,
            null,
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal(
            [chunks[3].Id, chunks[1].Id],
            results.Select(chunk => chunk.Id));
    }

    [Fact]
    public async Task SearchAsync_EmptyBookIds_ReturnsLibraryWideNearestChunks()
    {
        var firstBookId = Guid.NewGuid();
        var secondBookId = Guid.NewGuid();
        var chunks = new[]
        {
            CreateChunk(firstBookId, 0, [0f, 1f, 0f], "far chunk"),
            CreateChunk(secondBookId, 0, [0.8f, 0.6f, 0f], "second nearest chunk"),
            CreateChunk(firstBookId, 1, [-1f, 0f, 0f], "farthest chunk"),
            CreateChunk(secondBookId, 1, [1f, 0f, 0f], "nearest chunk")
        };
        await _writeRepository.ReplaceAsync(firstBookId, [chunks[0], chunks[2]], CancellationToken.None);
        await _writeRepository.ReplaceAsync(secondBookId, [chunks[1], chunks[3]], CancellationToken.None);

        var results = await _readRepository.SearchAsync(
            [1f, 0f, 0f],
            2,
            [],
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal(
            [chunks[3].Id, chunks[1].Id],
            results.Select(chunk => chunk.Id));
    }

    [Fact]
    public async Task SearchAsync_BookScope_ExcludesCloserChunksFromOtherBooks()
    {
        var bookId = Guid.NewGuid();
        var otherBookId = Guid.NewGuid();
        var scopedChunks = new[]
        {
            CreateChunk(bookId, 0, [0.8f, 0.6f, 0f], "scoped chunk one"),
            CreateChunk(bookId, 1, [0f, 1f, 0f], "scoped chunk two")
        };
        var closerChunk = CreateChunk(otherBookId, 0, [1f, 0f, 0f], "closer unscoped chunk");
        await _writeRepository.ReplaceAsync(bookId, scopedChunks, CancellationToken.None);
        await _writeRepository.ReplaceAsync(otherBookId, [closerChunk], CancellationToken.None);

        var results = await _readRepository.SearchAsync(
            [1f, 0f, 0f],
            10,
            [bookId],
            CancellationToken.None);

        Assert.Equal(scopedChunks.Select(chunk => chunk.Id), results.Select(chunk => chunk.Id));
        Assert.DoesNotContain(results, chunk => chunk.Id == closerChunk.Id);
    }

    [Fact]
    public async Task SearchAsync_MultipleBookIds_ReturnsOnlyChunksFromThoseBooks()
    {
        var firstBookId = Guid.NewGuid();
        var secondBookId = Guid.NewGuid();
        var unselectedBookId = Guid.NewGuid();
        var selectedChunks = new[]
        {
            CreateChunk(firstBookId, 0, [0.8f, 0.6f, 0f], "first selected chunk"),
            CreateChunk(secondBookId, 0, [0f, 1f, 0f], "second selected chunk")
        };
        var closerUnselectedChunk = CreateChunk(
            unselectedBookId,
            0,
            [1f, 0f, 0f],
            "closer unselected chunk");
        await _writeRepository.ReplaceAsync(firstBookId, [selectedChunks[0]], CancellationToken.None);
        await _writeRepository.ReplaceAsync(secondBookId, [selectedChunks[1]], CancellationToken.None);
        await _writeRepository.ReplaceAsync(unselectedBookId, [closerUnselectedChunk], CancellationToken.None);

        var results = await _readRepository.SearchAsync(
            [1f, 0f, 0f],
            10,
            [firstBookId, secondBookId],
            CancellationToken.None);

        Assert.Equal(
            selectedChunks.Select(chunk => chunk.Id),
            results.Select(chunk => chunk.Id));
        Assert.DoesNotContain(results, chunk => chunk.Id == closerUnselectedChunk.Id);
    }

    [Fact]
    public async Task ReplaceAsync_ChunkFromDifferentBook_ThrowsWithoutChangingStoredChunks()
    {
        var bookId = Guid.NewGuid();
        var existingChunk = CreateChunk(bookId, 0, [1f, 0f, 0f], "existing chunk");
        var replacementChunk = CreateChunk(bookId, 1, [0f, 1f, 0f], "replacement chunk");
        var mismatchedChunk = CreateChunk(Guid.NewGuid(), 0, [0f, 0f, 1f], "mismatched chunk");
        await _writeRepository.ReplaceAsync(bookId, [existingChunk], CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _writeRepository.ReplaceAsync(
                bookId,
                [replacementChunk, mismatchedChunk],
                CancellationToken.None));

        var results = await _readRepository.SearchAsync(
            [1f, 0f, 0f],
            10,
            [bookId],
            CancellationToken.None);

        Assert.Equal([existingChunk.Id], results.Select(chunk => chunk.Id));
    }

    private static DocumentChunk CreateChunk(
        Guid bookId,
        int chunkIndex,
        float[] embedding,
        string? content = null)
    {
        return DocumentChunk.Create(
            bookId,
            chunkIndex,
            chunkIndex + 1,
            content ?? $"Chunk {chunkIndex}",
            embedding,
            "TestProvider",
            "test-model");
    }
}
