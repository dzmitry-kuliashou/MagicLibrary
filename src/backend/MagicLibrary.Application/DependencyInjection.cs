using FluentValidation;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace MagicLibrary.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(CreateBookCommand).Assembly));
        services.AddValidatorsFromAssembly(typeof(CreateBookCommandValidator).Assembly);

        return services;
    }
}
