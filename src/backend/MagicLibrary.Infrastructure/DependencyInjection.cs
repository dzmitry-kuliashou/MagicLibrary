using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.KeywordExtraction;
using MagicLibrary.Application.PdfProcessing;
using MagicLibrary.Core.Repositories.Documents;
using MagicLibrary.Core.Repositories.Vectors;
using MagicLibrary.Infrastructure.Chat;
using MagicLibrary.Infrastructure.EmbeddingGeneration;
using MagicLibrary.Infrastructure.KeywordExtraction;
using MagicLibrary.Infrastructure.PdfProcessing;
using MagicLibrary.Infrastructure.Repositories.Documents;
using MagicLibrary.Infrastructure.Repositories.Vectors;
using MagicLibrary.Infrastructure.StorageSetup;
using JasperFx;
using Marten;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using OllamaSharp;
using AiEmbeddingGenerator = Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>;
using ApplicationEmbeddingGenerator = MagicLibrary.Application.EmbeddingGeneration.IEmbeddingGenerator;

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

        services.AddOptions<KeywordExtractionOptions>()
            .Bind(configuration.GetSection(KeywordExtractionOptions.SectionName))
            .Validate(options => options.MaxResults > 0,
                "KeywordExtraction:MaxResults must be greater than zero.")
            .Validate(options => options.MaxGeneratedTokens > 0,
                "KeywordExtraction:MaxGeneratedTokens must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<VectorSearchOptions>()
            .Bind(configuration.GetSection(VectorSearchOptions.SectionName))
            .Validate(options => options.TopK > 0,
                "VectorSearch:TopK must be greater than zero.")
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

        services.AddHttpClient("OllamaEmbedding", (serviceProvider, httpClient) =>
        {
            var embeddingOptions = serviceProvider.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
            httpClient.BaseAddress = new Uri(embeddingOptions.BaseUrl);
        });
        services.AddTransient<AiEmbeddingGenerator>(serviceProvider =>
        {
            var client = new OllamaApiClient(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("OllamaEmbedding"));
            client.SelectedModel = serviceProvider
                .GetRequiredService<IOptions<EmbeddingOptions>>()
                .Value.Model;
            return client;
        });
        services.AddTransient<ApplicationEmbeddingGenerator, EmbeddingGenerator>();

        var chatBaseUrl = configuration["Chat:BaseUrl"];
        if (!Uri.TryCreate(chatBaseUrl, UriKind.Absolute, out var chatBaseAddress) ||
            (chatBaseAddress.Scheme != Uri.UriSchemeHttp && chatBaseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Chat:BaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        var chatModel = configuration["Chat:Model"];
        if (string.IsNullOrWhiteSpace(chatModel))
        {
            throw new InvalidOperationException("Chat:Model must be configured.");
        }

        services.AddHttpClient("OllamaChat", (_, httpClient) =>
        {
            httpClient.BaseAddress = chatBaseAddress;
            httpClient.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddTransient<IChatClient>(serviceProvider =>
        {
            var client = new OllamaApiClient(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("OllamaChat"));
            client.SelectedModel = chatModel;
            return client;
        });
        services.AddTransient<ILibraryChatAgent, LibraryChatAgent>();
        services.AddTransient<IKeywordExtractor, KeywordExtractor>();

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
