using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record UpdateBookMetadataCommand(
    Guid BookId,
    string Title,
    string? Author) : IRequest<ErrorOr<Book>>;

public sealed class UpdateBookMetadataCommandHandler(
    IValidator<UpdateBookMetadataCommand> validator,
    IBookReadRepository bookReadRepository,
    IBookWriteRepository bookWriteRepository)
    : IRequestHandler<UpdateBookMetadataCommand, ErrorOr<Book>>
{
    public async Task<ErrorOr<Book>> Handle(
        UpdateBookMetadataCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return validationResult.Errors.ToErrors();
        }

        var book = await bookReadRepository.GetByIdAsync(request.BookId, cancellationToken);
        if (book is null)
        {
            return BookErrors.BookNotFound(request.BookId);
        }
        book.UpdateMetadata(request.Title, request.Author ?? string.Empty);
        book.UpdateMetadata(request.Title, request.Author ?? string.Empty);
        return await bookWriteRepository.UpdateAsync(book, cancellationToken);
    }
}
