using MagicLibrary.Core.Repositories;
using MagicLibrary.Infrastructure.Repositories;
using MagicLibrary.Infrastructure.StorageSetup;
using JasperFx;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
