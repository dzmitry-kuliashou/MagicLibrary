using System.Security.Cryptography;
using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Exceptions;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;
using Microsoft.Extensions.Options;

namespace MagicLibrary.Application.Commands;

public sealed record CreateBookCommand(
    string FileName,
    string Title,
    string? Author = null) : IRequest<ErrorOr<Book>>;

public sealed class CreateBookCommandHandler(
    IValidator<CreateBookCommand> validator,
    IOptions<BookStorageOptions> storageOptions,
    IBookReadRepository bookReadRepository,
    IBookWriteRepository bookWriteRepository)
    : IRequestHandler<CreateBookCommand, ErrorOr<Book>>
{
    public async Task<ErrorOr<Book>> Handle(
        CreateBookCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return validationResult.Errors.ToErrors();
        }

        var rootDirectory = storageOptions.Value.RootDirectory;
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return BookErrors.MissingStoragePath;
        }

        var fullRootPath = Path.GetFullPath(rootDirectory);
        var fullFilePath = Path.GetFullPath(Path.Combine(fullRootPath, request.FileName));
        var relativeFilePath = Path.GetRelativePath(fullRootPath, fullFilePath);
        if (IsOutsideRoot(relativeFilePath))
        {
            return BookErrors.UnsafeFileName;
        }

        if (!File.Exists(fullFilePath))
        {
            return BookErrors.FileNotFound;
        }

        if (await bookReadRepository.BookExistsAsync(request.FileName, cancellationToken))
        {
            return BookErrors.DuplicateFileName;
        }

        byte[] fileBytes;
        try
        {
            fileBytes = await File.ReadAllBytesAsync(fullFilePath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return BookErrors.FileNotFound;
        }

        var contentHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
        var book = Book.Create(
            request.FileName,
            request.Title,
            request.Author ?? string.Empty,
            contentHash,
            DateTimeOffset.UtcNow);

        try
        {
            return await bookWriteRepository.CreateAsync(book, cancellationToken);
        }
        catch (AggregateAlreadyExistsException)
        {
            return BookErrors.DuplicateFileName;
        }
    }

    private static bool IsOutsideRoot(string relativeFilePath)
    {
        return Path.IsPathRooted(relativeFilePath)
            || relativeFilePath.Equals("..", StringComparison.Ordinal)
            || relativeFilePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)
            || relativeFilePath.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal);
    }
}
