using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.Errors;
using MagicLibrary.Application.Validation;
using Microsoft.Extensions.Options;

namespace MagicLibrary.Application.Validators;

public sealed class ReplaceDocumentChunksCommandValidator
    : AbstractValidator<ReplaceDocumentChunksCommand>
{
    public ReplaceDocumentChunksCommandValidator(IOptions<EmbeddingOptions> embeddingOptions)
    {
        var dimensions = embeddingOptions.Value.Dimensions;

        RuleFor(command => command.BookId)
            .NotEmpty()
            .WithError(DocumentChunkErrors.InvalidBookId);

        RuleFor(command => command.Chunks)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithError(DocumentChunkErrors.MissingChunks)
            .NotEmpty()
            .WithError(DocumentChunkErrors.MissingChunks);

        RuleForEach(command => command.Chunks)
            .ChildRules(chunk =>
            {
                chunk.RuleFor(input => input.ChunkIndex)
                    .GreaterThanOrEqualTo(0)
                    .WithError(DocumentChunkErrors.InvalidChunkIndex);

                chunk.RuleFor(input => input.PageNumber)
                    .GreaterThan(0)
                    .WithError(DocumentChunkErrors.InvalidPageNumber);

                chunk.RuleFor(input => input.Content)
                    .Must(content => !string.IsNullOrWhiteSpace(content))
                    .WithError(DocumentChunkErrors.MissingContent);

                chunk.RuleFor(input => input.Embedding)
                    .Cascade(CascadeMode.Stop)
                    .NotNull()
                    .WithError(DocumentChunkErrors.MissingEmbedding)
                    .Must(embedding => embedding.Count > 0)
                    .WithError(DocumentChunkErrors.MissingEmbedding)
                    .Must(embedding => embedding.Count == dimensions)
                    .WithError(DocumentChunkErrors.InvalidEmbeddingDimensions(dimensions));
            });
    }
}
