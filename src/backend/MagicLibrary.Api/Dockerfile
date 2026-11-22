FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/backend/MagicLibrary.Core/ src/backend/MagicLibrary.Core/
COPY src/backend/MagicLibrary.Contract/ src/backend/MagicLibrary.Contract/
COPY src/backend/MagicLibrary.Application/ src/backend/MagicLibrary.Application/
COPY src/backend/MagicLibrary.Infrastructure/ src/backend/MagicLibrary.Infrastructure/
COPY src/backend/MagicLibrary.Api/ src/backend/MagicLibrary.Api/

RUN dotnet restore "src/backend/MagicLibrary.Api/MagicLibrary.Api.csproj"
RUN dotnet publish "src/backend/MagicLibrary.Api/MagicLibrary.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MagicLibrary.Api.dll"]
