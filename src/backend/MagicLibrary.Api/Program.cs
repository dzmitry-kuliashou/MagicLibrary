using MagicLibrary.Application;
using MagicLibrary.Application.ConfigurationOptions;
using MagicLibrary.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://localhost:5100");
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<BookStorageOptions>(
    builder.Configuration.GetSection(BookStorageOptions.SectionName));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

await app.RunAsync();
