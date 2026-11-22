# MagicLibrary
# MagicLibrary

## Run the API and PostgreSQL with Docker Compose

From the repository root, start the API and pgvector database with:

```sh
docker compose up --build -d
```

The API listens on `http://localhost:8080`. The Compose stack includes one Ollama service shared by chat and embeddings. The API reaches it at `http://ollama:11434`; local tools and direct API runs can reach it at `http://localhost:11435`.

Pull the models explicitly after the services start:

```sh
docker compose exec ollama ollama pull qwen3:4b
docker compose exec ollama ollama pull nomic-embed-text:latest
```

Ollama stores model files in the persistent `ollama-data` volume, so they remain available when the container is recreated. PostgreSQL data is stored separately in a named volume, and the vector schema is initialized when that volume is first created. `docker compose down -v` removes both data volumes.

The Compose stack accepts these optional environment variables:

| Variable | Default | Purpose |
|---|---|---|
| `POSTGRES_PASSWORD` | `magiclibrary-local` | Local PostgreSQL password; use a different value for local environments that need it. |
| `CHAT_MODEL` | `qwen3:4b` | Chat model served by Ollama. Set this before starting Compose to choose another model, and pull that model into the Ollama volume explicitly. |
| `EMBEDDING_PROVIDER` | `Ollama` | Selected embedding provider name. |
| `EMBEDDING_MODEL` | `nomic-embed-text:latest` | Selected embedding model. |
| `EMBEDDING_DIMENSIONS` | `768` | The model's output dimension; must be between 1 and 2000 for the HNSW vector index. |

For example, in PowerShell, select another chat model before starting the stack, then pull it:

```powershell
$env:CHAT_MODEL = "qwen3:8b"
docker compose up --build -d
docker compose exec ollama ollama pull qwen3:8b
```

The chat model can be changed independently of the embedding model; both use the same Ollama service, and changing the chat model does not affect the embedding settings. Use one embedding model per database, and ensure its output dimension matches `EMBEDDING_DIMENSIONS`. Changing dimensions requires recreating the database schema and re-embedding stored chunks. PostgreSQL initialization scripts run only for a fresh data volume.

## Run the API directly

`src/backend/MagicLibrary.Api/appsettings.json` contains non-production localhost defaults for PostgreSQL, chat, and embedding settings. The chat defaults are `http://localhost:11435` and `qwen3:4b`; the embedding defaults are `http://localhost:11435`, `nomic-embed-text:latest`, and 768 dimensions. Override them with .NET environment variables as needed. For example, in PowerShell:

```powershell
$env:ConnectionStrings__MagicLibrary = "Host=localhost;Port=5432;Database=magiclibrary;Username=magiclibrary;Password=your-local-password"
$env:Embedding__Provider = "Ollama"
$env:Chat__BaseUrl = "http://localhost:11435"
$env:Chat__Model = "qwen3:4b"
$env:Embedding__BaseUrl = "http://localhost:11435"
$env:Embedding__Model = "nomic-embed-text:latest"
$env:Embedding__Dimensions = "768"
dotnet run --project src/backend/MagicLibrary.Api/MagicLibrary.Api.csproj
```

The chat and embedding settings configure model defaults only; chat and embedding client integrations are not included in this task.
