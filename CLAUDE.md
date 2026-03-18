# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the app
dotnet run --project WaterDrop/WaterDrop.csproj
# HTTP: http://localhost:5282 | HTTPS: https://localhost:7292

# Build
dotnet build WaterDrop/WaterDrop.csproj

# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter "Category=Unit"

# Run only integration tests (excluding stress tests)
dotnet test --filter "Category=Integration&FullyQualifiedName!~StressTest"
```

## Architecture

WaterDrop is a **Blazor Web App (.NET 10, Interactive Server mode)** that lets users find public toilets in a city using the [Overpass API](https://overpass-api.de) (OpenStreetMap data) and display them on a Leaflet map. Users can also add comments and picture URLs to toilets, which are persisted in SQL Server.

### Data Flow

1. User enters a city name in `Home.razor`
2. `ToiletQueryBuilder` constructs an Overpass Query Language (OQL) string
3. `kloService.GetToilets()` POSTs the query to `https://overpass-api.de/api/interpreter`
4. Response is deserialized into `KloModel` (with nested `Element` list)
5. JS interop calls `addMarker()` in `wwwroot/map.js` for each element, rendering Leaflet markers
6. Comments/pictures from `DatabaseKloModel` (SQL Server via EF Core) are loaded and shown in marker popups

### Key Files

| File | Purpose |
|------|---------|
| `WaterDrop/Components/Pages/Home.razor` | Main UI: city search, map container, data table, CRUD actions |
| `WaterDrop/Components/Services/kloService.cs` | All business logic: Overpass API calls + EF Core CRUD |
| `WaterDrop/Components/Services/toiletQueryBuilder.cs` | Builder pattern for constructing OQL queries |
| `WaterDrop/Components/Data/ApplicationDbContext.cs` | EF Core DbContext — only `DatabaseKloModel` is persisted |
| `WaterDrop/Components/Models/KloModel.cs` | API response models (`KloModel`, `Element`, `Osm3s`) |
| `WaterDrop/Components/Models/DatabaseKloModel.cs` | App-specific model stored in DB (comment + pictureUrl keyed by OSM `elementId`) |
| `WaterDrop/wwwroot/map.js` | Leaflet map functions: `initMap`, `addMarker`, `centerMap`, `clearMarkers` |
| `WaterDrop/Program.cs` | DI setup — `kloService` is Scoped, DbContext uses "DefaultConnection" |

### Two Models

- **`KloModel` / `Element`** — Represents data from the Overpass API. Not persisted in the DB (only used in-memory during a session).
- **`DatabaseKloModel`** — Stores user-submitted `Comment` and `PictureUrl` for a toilet, linked by `ElementId` (OSM node/way ID). This is what lives in SQL Server.

### Configuration

Copy `appsettings.TEMPLATE.json` to `appsettings.json` and set `ConnectionStrings.DatabaseConnection`. The development environment uses `appsettings.Development.json` (no connection string — the app falls back to In-Memory DB for local dev/testing).

### Testing

Tests are in `WaterDropTests/`. Both unit and integration tests use EF Core's **In-Memory database** (each test creates an isolated context with a unique DB name). Moq is available but the current tests test directly against the service + in-memory DB. Test categories are declared with `[Trait("Category", "Unit")]` or `[Trait("Category", "Integration")]`.

### CI/CD

Two GitHub Actions workflows mirror the two branches:
- `development.yml` → deploys to Azure dev slot on push to `development`
- `main.yml` → deploys to Azure production slot on push to `master`

Both pipelines run unit and integration tests in parallel before deploying. Secrets `AZURE_WEBAPP_PUBLISH_PROFILE` and `AZURE_WEBAPP_PUBLISH_PROFILE_PROD` must be set in GitHub repository secrets.
