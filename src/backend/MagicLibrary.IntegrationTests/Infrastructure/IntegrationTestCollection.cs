using Xunit;

namespace MagicLibrary.IntegrationTests.Infrastructure;

[CollectionDefinition("PostgreSQL integration tests", DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL integration tests";
}
