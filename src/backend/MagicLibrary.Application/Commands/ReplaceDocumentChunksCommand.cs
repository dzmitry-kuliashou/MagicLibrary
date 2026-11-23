using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Vectors;
using MediatR;
using Microsoft.Extensions.Options;

namespace MagicLibrary.Application.Commands;

public sealed record ReplaceDocumentChunksCommand(
    Guid BookId,
    IReadOnlyList<ReplaceDocumentChunkInput> Chunks) : IRequest<ErrorOr<Success>>;

public sealed record ReplaceDocumentChunkInput(
    int ChunkIndex,
    int PageNumber,
    string Content,
    IReadOnlyList<float> Embedding);

public sealed class ReplaceDocumentChunksCommandHandler(
    IValidator<ReplaceDocumentChunksCommand> validator,
    IOptions<EmbeddingOptions> embeddingOptions,
    IDocumentChunkWriteRepository documentChunkWriteRepository)
    : IRequestHandler<ReplaceDocumentChunksCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        ReplaceDocumentChunksCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return validationResult.Errors.ToErrors();
        }

        var options = embeddingOptions.Value;
        var chunks = request.Chunks
            .Select(input => DocumentChunk.Create(
                request.BookId,
                input.ChunkIndex,
                input.PageNumber,
                input.Content,
                input.Embedding,
                options.Provider,
                options.Model))
            .ToArray();

        await documentChunkWriteRepository.ReplaceAsync(
            request.BookId,
            chunks,
            cancellationToken);

        return Result.Success;
    }
}
