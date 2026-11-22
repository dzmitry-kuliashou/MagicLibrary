using MagicLibrary.Application;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.Configure<BookStorageOptions>(
    builder.Configuration.GetSection(BookStorageOptions.SectionName));

var app = builder.Build();

await app.RunAsync();
