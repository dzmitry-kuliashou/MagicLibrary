using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.EmbeddingGeneration;
using MagicLibrary.Application.KeywordExtraction;
using MagicLibrary.Application.PdfProcessing;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Core.Repositories.Vectors;
using MagicLibrary.Infrastructure.EmbeddingGeneration;
using MagicLibrary.Infrastructure.KeywordExtraction;
using MagicLibrary.Infrastructure.PdfProcessing;
using MagicLibrary.Infrastructure.Repositories.Documents;
using MagicLibrary.Infrastructure.Repositories.Vectors;
using MagicLibrary.Infrastructure.StorageSetup;
using JasperFx;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

        services.AddOptions<KeywordExtractionOptions>()
            .Bind(configuration.GetSection(KeywordExtractionOptions.SectionName))
            .Validate(options => options.MaxResults > 0,
                "KeywordExtraction:MaxResults must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection(EmbeddingOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Provider),
                "Embedding:Provider must be configured.")
            .Validate(options =>
                    Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress) &&
                    (baseAddress.Scheme == Uri.UriSchemeHttp || baseAddress.Scheme == Uri.UriSchemeHttps),
                "Embedding:BaseUrl must be an absolute HTTP or HTTPS URL.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model),
                "Embedding:Model must be configured.")
            .Validate(options => options.Dimensions > 0,
                "Embedding:Dimensions must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient<OllamaEmbeddingGenerator>((serviceProvider, httpClient) =>
        {
            var embeddingOptions = serviceProvider.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
            httpClient.BaseAddress = new Uri(embeddingOptions.BaseUrl);
        });
        services.AddTransient<IEmbeddingGenerator>(serviceProvider =>
            serviceProvider.GetRequiredService<OllamaEmbeddingGenerator>());
        services.AddTransient<IEmbeddingGeneratorFactory, EmbeddingGeneratorFactory>();

        var chatBaseUrl = configuration["Chat:BaseUrl"];
        if (!Uri.TryCreate(chatBaseUrl, UriKind.Absolute, out var chatBaseAddress) ||
            (chatBaseAddress.Scheme != Uri.UriSchemeHttp && chatBaseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Chat:BaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(configuration["Chat:Model"]))
        {
            throw new InvalidOperationException("Chat:Model must be configured.");
        }

        services.AddHttpClient<OllamaKeywordExtractor>((_, httpClient) =>
        {
            httpClient.BaseAddress = chatBaseAddress;
        });
        services.AddTransient<IKeywordExtractor>(serviceProvider =>
            serviceProvider.GetRequiredService<OllamaKeywordExtractor>());

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
