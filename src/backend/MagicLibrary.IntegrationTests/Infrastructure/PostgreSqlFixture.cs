using System.Globalization;
using MagicLibrary.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace MagicLibrary.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public const int EmbeddingDimensions = 3;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("magiclibrary_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var repositoryRoot = FindRepositoryRoot();
        var schemaPath = Path.Combine(
            repositoryRoot,
            "src",
            "backend",
            "MagicLibrary.Infrastructure",
            "StorageSetup",
            "DocumentChunks.sql");
        var schemaTemplate = await File.ReadAllTextAsync(schemaPath);
        var schema = schemaTemplate.Replace(
            "@@EMBEDDING_DIMENSIONS@@",
            EmbeddingDimensions.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(schema, connection);
        await command.ExecuteNonQueryAsync();
    }

    public ServiceProvider CreateServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MagicLibrary"] = ConnectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MagicLibrary.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate MagicLibrary.sln from the integration-test output directory.");
    }
}
