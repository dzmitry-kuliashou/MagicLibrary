using MagicLibrary.Application.Queries;
using MagicLibrary.Core.Repositories.Documents;
using Moq;
using Xunit;

namespace MagicLibrary.UnitTests.Application.Queries;

public sealed class SearchBooksQueryHandlerTests
{
    private readonly Mock<IBookReadRepository> _bookReadRepository = new();

    private SearchBooksQueryHandler CreateHandler()
    {
        return new SearchBooksQueryHandler(_bookReadRepository.Object);
    }

    [Fact]
    public async Task Handle_SearchesFieldsInOrderAndReturnsDistinctSortedIds()
    {
        var terms = new[] { "Alpha Term", "Bravo!" };
        var titleIds = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("00000000-0000-0000-0000-000000000001")
        };
        var authorIds = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            titleIds[0]
        };
        var keywordIds = new[]
        {
            titleIds[1],
            Guid.Parse("00000000-0000-0000-0000-000000000004")
        };
        var sequence = new MockSequence();
        _bookReadRepository
            .InSequence(sequence)
            .Setup(repository => repository.SearchByTitleAsync(
                It.Is<IReadOnlyCollection<string>>(searchTerms => searchTerms.SequenceEqual(terms)),
                CancellationToken.None))
            .ReturnsAsync(titleIds);
        _bookReadRepository
            .InSequence(sequence)
            .Setup(repository => repository.SearchByAuthorAsync(
                It.Is<IReadOnlyCollection<string>>(searchTerms => searchTerms.SequenceEqual(terms)),
                CancellationToken.None))
            .ReturnsAsync(authorIds);
        _bookReadRepository
            .InSequence(sequence)
            .Setup(repository => repository.SearchByKeywordsAsync(
                It.Is<IReadOnlyCollection<string>>(keywords =>
                    keywords.SequenceEqual(new[] { "alpha_term", "bravo" })),
                CancellationToken.None))
            .ReturnsAsync(keywordIds);
        var handler = CreateHandler();

        var result = await handler.Handle(new SearchBooksQuery(terms), CancellationToken.None);

        Assert.Equal(
            [
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Guid.Parse("00000000-0000-0000-0000-000000000004")
            ],
            result.BookIds);
    }

    [Fact]
    public async Task Handle_EmptyOrBlankTerms_ReturnsEmptyIds()
    {
        _bookReadRepository
            .Setup(repository => repository.SearchByTitleAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                CancellationToken.None))
            .ReturnsAsync((IReadOnlyList<Guid>)Array.Empty<Guid>());
        _bookReadRepository
            .Setup(repository => repository.SearchByAuthorAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                CancellationToken.None))
            .ReturnsAsync((IReadOnlyList<Guid>)Array.Empty<Guid>());
        var termsSets = new IReadOnlyList<string>?[]
        {
            Array.Empty<string>(),
            new[] { string.Empty },
            new[] { " ", "\t" },
            new[] { "!!!" },
            null
        };
        var handler = CreateHandler();

        foreach (var terms in termsSets)
        {
            var result = await handler.Handle(
                new SearchBooksQuery(terms),
                CancellationToken.None);

            Assert.Empty(result.BookIds);
        }

        _bookReadRepository.Verify(
            repository => repository.SearchByKeywordsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                CancellationToken.None),
            Times.Never);
    }
}
