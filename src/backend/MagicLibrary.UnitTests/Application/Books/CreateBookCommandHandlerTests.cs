using System.Security.Cryptography;
using ErrorOr;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validators;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Exceptions;
using MagicLibrary.Core.Repositories.Documents;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Books;

public sealed class CreateBookCommandHandlerTests
{
    private static CreateBookCommand CreateCommand(
        string fileName = "book.pdf",
        string title = "Title",
        string? author = null)
    {
        return new CreateBookCommand(fileName, title, author);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsBookWithRawByteHash()
    {
        using var scope = new TestScope();
        const string fileName = "book.pdf";
        const string title = "Supplied title";
        var fileBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x00, 0xFF };
        await File.WriteAllBytesAsync(
            Path.Combine(scope.TemporaryRootDirectory, fileName),
            fileBytes);

        var result = await scope.Handler.Handle(
            CreateCommand(fileName, title),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(fileName, result.Value.SourceFileName);
        Assert.Equal(title, result.Value.Title);
        Assert.Equal("", result.Value.Author);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant(),
            result.Value.ContentHash);
        scope.WriteRepository.Verify(repository => repository.CreateAsync(
            It.Is<Book>(book =>
                book.SourceFileName == fileName
                && book.Title == title
                && book.Author == ""
                && book.ContentHash == result.Value.ContentHash),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_MissingStorageConfiguration_ReturnsConfigurationError()
    {
        using var scope = new TestScope(rootDirectory: " ");

        var result = await scope.Handler.Handle(CreateCommand(), CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(BookErrors.MissingStoragePath.Code, error.Code);
        scope.ReadRepository.Verify(
            repository => repository.BookExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        scope.WriteRepository.Verify(
            repository => repository.CreateAsync(
                It.IsAny<Book>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_MissingFile_ReturnsFileNotFound()
    {
        using var scope = new TestScope();

        var result = await scope.Handler.Handle(
            CreateCommand("missing.pdf"),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(BookErrors.FileNotFound.Code, error.Code);
        scope.ReadRepository.Verify(
            repository => repository.BookExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        scope.WriteRepository.Verify(
            repository => repository.CreateAsync(
                It.IsAny<Book>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidCommand_ReturnsValidationError()
    {
        using var scope = new TestScope();

        var result = await scope.Handler.Handle(
            CreateCommand(title: " "),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(BookErrors.MissingTitle.Code, error.Code);
        Assert.Equal(BookErrors.MissingTitle.Description, error.Description);
        Assert.Equal(nameof(CreateBookCommand.Title), error.Metadata?["PropertyName"]);
        scope.ReadRepository.Verify(
            repository => repository.BookExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        scope.WriteRepository.Verify(
            repository => repository.CreateAsync(
                It.IsAny<Book>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateFileName_ReturnsConflict()
    {
        using var scope = new TestScope();
        const string fileName = "book.pdf";
        await File.WriteAllBytesAsync(
            Path.Combine(scope.TemporaryRootDirectory, fileName),
            [0x25, 0x50, 0x44, 0x46]);
        scope.ReadRepository
            .Setup(repository => repository.BookExistsAsync(fileName, CancellationToken.None))
            .ReturnsAsync(true);

        var result = await scope.Handler.Handle(
            CreateCommand(fileName),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(BookErrors.DuplicateFileName.Code, error.Code);
        scope.ReadRepository.Verify(
            repository => repository.BookExistsAsync(fileName, CancellationToken.None),
            Times.Once);
        scope.WriteRepository.Verify(
            repository => repository.CreateAsync(
                It.IsAny<Book>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PersistenceDuplicateRace_ReturnsConflict()
    {
        using var scope = new TestScope();
        const string fileName = "book.pdf";
        await File.WriteAllBytesAsync(
            Path.Combine(scope.TemporaryRootDirectory, fileName),
            [0x25, 0x50, 0x44, 0x46]);
        scope.WriteRepository
            .Setup(repository => repository.CreateAsync(
                It.IsAny<Book>(),
                CancellationToken.None))
            .ThrowsAsync(new AggregateAlreadyExistsException(Guid.NewGuid()));

        var result = await scope.Handler.Handle(
            CreateCommand(fileName),
            CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(BookErrors.DuplicateFileName.Code, error.Code);
        scope.WriteRepository.Verify(
            repository => repository.CreateAsync(
                It.IsAny<Book>(),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CancellationDuringBookLookup_Propagates()
    {
        using var scope = new TestScope();
        const string fileName = "book.pdf";
        await File.WriteAllBytesAsync(
            Path.Combine(scope.TemporaryRootDirectory, fileName),
            [0x25, 0x50, 0x44, 0x46]);
        using var cancellationSource = new CancellationTokenSource();
        scope.ReadRepository
            .Setup(repository => repository.BookExistsAsync(
                fileName,
                cancellationSource.Token))
            .Returns(async (string _, CancellationToken cancellationToken) =>
            {
                await Task.Yield();
                cancellationSource.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.Handler.Handle(CreateCommand(fileName), cancellationSource.Token));

        scope.WriteRepository.Verify(
            repository => repository.CreateAsync(
                It.IsAny<Book>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class TestScope : IDisposable
    {
        public TestScope(string? rootDirectory = null)
        {
            TemporaryRootDirectory = Path.Combine(
                Path.GetTempPath(),
                $"MagicLibrary-{Guid.NewGuid():N}");
            Directory.CreateDirectory(TemporaryRootDirectory);

            var options = Options.Create(new BookStorageOptions
            {
                RootDirectory = rootDirectory ?? TemporaryRootDirectory
            });
            Handler = new CreateBookCommandHandler(
                new CreateBookCommandValidator(),
                options,
                ReadRepository.Object,
                WriteRepository.Object);

            ReadRepository
                .Setup(repository => repository.BookExistsAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            WriteRepository
                .Setup(repository => repository.CreateAsync(
                    It.IsAny<Book>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Book aggregate, CancellationToken _) => Task.FromResult(aggregate));
        }

        public string TemporaryRootDirectory { get; }

        public Mock<IBookReadRepository> ReadRepository { get; } = new();

        public Mock<IBookWriteRepository> WriteRepository { get; } = new();

        public CreateBookCommandHandler Handler { get; }

        public void Dispose()
        {
            if (Directory.Exists(TemporaryRootDirectory))
            {
                Directory.Delete(TemporaryRootDirectory, recursive: true);
            }
        }
    }
}
