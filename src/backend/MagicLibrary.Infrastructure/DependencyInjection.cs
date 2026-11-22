using MagicLibrary.Application.PdfProcessing;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Core.Repositories.Vectors;
using MagicLibrary.Infrastructure.PdfProcessing;
using MagicLibrary.Infrastructure.Repositories.Documents;
using MagicLibrary.Infrastructure.Repositories.Vectors;
using MagicLibrary.Infrastructure.StorageSetup;
using JasperFx;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace MagicLibrary.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MagicLibrary");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'MagicLibrary' must be configured.");
        }

        services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            return dataSourceBuilder.Build();
        });
        services.AddHostedService<EnsurePgTrgmExtensionHostedService>();

        services.AddMarten(options =>
            {
                options.Connection(connectionString);
                options.Schema.Include<MagicLibraryDocumentRegistry>();
                options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
            })
            .UseLightweightSessions();

        services.AddScoped<IBookReadRepository, BookReadRepository>();
        services.AddScoped<IBookWriteRepository, BookWriteRepository>();
        services.AddScoped<IIndexingJobReadRepository, IndexingJobReadRepository>();
        services.AddScoped<IIndexingJobWriteRepository, IndexingJobWriteRepository>();
        services.AddScoped<IConversationReadRepository, ConversationReadRepository>();
        services.AddScoped<IConversationWriteRepository, ConversationWriteRepository>();
        services.AddScoped<IDocumentChunkReadRepository, DocumentChunkReadRepository>();
        services.AddScoped<IDocumentChunkWriteRepository, DocumentChunkWriteRepository>();
        services.AddScoped<IPdfTextExtractor, PdfPigTextExtractor>();

        return services;
    }
}

internal sealed class EnsurePgTrgmExtensionHostedService(NpgsqlDataSource dataSource) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
