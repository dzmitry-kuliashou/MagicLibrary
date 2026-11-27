using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Vectors;
using MediatR;

namespace MagicLibrary.Application.Queries;

public sealed record SearchDocumentChunksQuery(
    IReadOnlyList<float> QueryEmbedding,
    IReadOnlyCollection<Guid>? BookIds,
    int TopK) : IRequest<IReadOnlyList<DocumentChunk>>;

public sealed class SearchDocumentChunksQueryHandler(IDocumentChunkReadRepository repository)
    : IRequestHandler<SearchDocumentChunksQuery, IReadOnlyList<DocumentChunk>>
{
    public Task<IReadOnlyList<DocumentChunk>> Handle(
        SearchDocumentChunksQuery request,
        CancellationToken cancellationToken)
    {
        return repository.SearchAsync(
            request.QueryEmbedding,
            request.TopK,
            request.BookIds,
            cancellationToken);
    }
}
