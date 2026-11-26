using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Conversations;
using MagicLibrary.Application.Indexing;
using MagicLibrary.Application.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MagicLibrary.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(CreateBookCommand).Assembly));
        services.AddValidatorsFromAssembly(typeof(CreateBookCommandValidator).Assembly);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ILibraryConversationOrchestrator, LibraryConversationOrchestrator>();
        services.AddSingleton<IIndexingJobBackgroundProcessor, IndexingJobBackgroundProcessor>();
        services.AddScoped<IndexingWorker>();
        services.AddScoped<IndexingScenarioFactory>();
        services.AddScoped<IIndexingScenario, LibraryIndexingScenario>();
        services.AddScoped<IIndexingScenario, BookIndexingScenario>();

        return services;
    }
}
