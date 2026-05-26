using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WaterDrop.Components.Data;
using WaterDrop.Components.Models;

namespace WaterDrop.Components.Services
{
    /// <summary>
    /// Background service that runs once on app startup. Queries the Overpass API
    /// for amenity=drinking_water in every city bounding box and inserts new
    /// facilities into the ToiletData table. Existing rows (matched by ElementId)
    /// are skipped, so this is safe to re-run on every restart.
    /// </summary>
    public class DrinkingWaterSeeder : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DrinkingWaterSeeder> _logger;

        private static readonly HttpClient Http;

        static DrinkingWaterSeeder()
        {
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            Http.DefaultRequestHeaders.Add("User-Agent", "WaterDrop/1.0 (https://github.com/waterdrop)");
            Http.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public DrinkingWaterSeeder(IServiceScopeFactory scopeFactory, ILogger<DrinkingWaterSeeder> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Small delay so the app is fully started before we hit the DB / API
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            _logger.LogInformation("DrinkingWaterSeeder starting...");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var geocoding = scope.ServiceProvider.GetRequiredService<IGeocodingService>();

                var cities = geocoding.GetAllCities();
                if (cities.Count == 0)
                {
                    _logger.LogWarning("No cities available — skipping seed");
                    return;
                }

                int totalInserted = 0;

                foreach (var (city, bbox) in cities)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        var inserted = await SeedCityAsync(context, city, bbox, stoppingToken);
                        if (inserted > 0)
                        {
                            totalInserted += inserted;
                            _logger.LogInformation("Seeded {Count} drinking water facilities for {City}", inserted, city);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to seed drinking water for {City} — skipping", city);
                    }

                    // Rate-limit: Overpass API asks for max 1 request per second
                    await Task.Delay(TimeSpan.FromMilliseconds(1500), stoppingToken);
                }

                // Clear the in-memory cache so searches pick up newly seeded data
                if (totalInserted > 0)
                {
                    var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
                    foreach (var cityKey in cities.Keys)
                        cache.Remove($"toilets_{cityKey}");
                    _logger.LogInformation("Cleared city caches after seeding");
                }

                _logger.LogInformation("DrinkingWaterSeeder finished. Total new facilities: {Count}", totalInserted);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("DrinkingWaterSeeder cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DrinkingWaterSeeder failed");
            }
        }

        private async Task<int> SeedCityAsync(ApplicationDbContext context, string city, BoundingBox bbox, CancellationToken ct)
        {
            // Check if we already have drinking water data in this bbox
            var existingCount = await context.Toilets
                .Where(t => t.Lat >= bbox.MinLat && t.Lat <= bbox.MaxLat &&
                            t.Lon >= bbox.MinLon && t.Lon <= bbox.MaxLon &&
                            t.TagsJson != null && EF.Functions.Like(t.TagsJson, "%\"amenity\":\"drinking_water\"%"))
                .CountAsync(ct);

            if (existingCount > 0)
            {
                _logger.LogDebug("{City}: already has {Count} drinking water rows — skipping", city, existingCount);
                return 0;
            }

            // Query Overpass API — coordinates MUST use period as decimal separator
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var bboxStr = string.Format(inv, "{0},{1},{2},{3}",
                bbox.MinLat, bbox.MinLon, bbox.MaxLat, bbox.MaxLon);
            var query = $"[out:json][timeout:15];node[\"amenity\"=\"drinking_water\"]({bboxStr});out;";

            var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("data", query) });
            var response = await Http.PostAsync("https://overpass-api.de/api/interpreter", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Overpass API returned {Status} for {City}: {Body}",
                    (int)response.StatusCode, city, body.Length > 200 ? body[..200] : body);
                return 0;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var elements = doc.RootElement.GetProperty("elements");

            int inserted = 0;

            foreach (var el in elements.EnumerateArray())
            {
                if (el.GetProperty("type").GetString() != "node") continue;

                var osmId = el.GetProperty("id").GetInt64();
                var lat = el.GetProperty("lat").GetDouble();
                var lon = el.GetProperty("lon").GetDouble();

                // Skip if already exists
                var exists = await context.Toilets.AnyAsync(t => t.ElementId == osmId, ct);
                if (exists) continue;

                // Build tags dictionary
                var tags = new Dictionary<string, string> { ["amenity"] = "drinking_water" };
                if (el.TryGetProperty("tags", out var tagsEl))
                {
                    foreach (var prop in tagsEl.EnumerateObject())
                    {
                        tags[prop.Name] = prop.Value.GetString() ?? "";
                    }
                }

                var toilet = new ToiletData
                {
                    Id = Guid.NewGuid(),
                    ElementId = osmId,
                    Lat = lat,
                    Lon = lon,
                    Type = "node",
                    City = city,
                    Name = tags.GetValueOrDefault("name"),
                    IsAccessible = tags.TryGetValue("wheelchair", out var wc) && wc == "yes",
                    HasFee = tags.TryGetValue("fee", out var fee) && fee == "yes",
                    OpeningHours = tags.GetValueOrDefault("opening_hours"),
                    Tags = tags,
                    ImportedAt = DateTime.UtcNow
                };

                context.Toilets.Add(toilet);
                inserted++;
            }

            if (inserted > 0)
                await context.SaveChangesAsync(ct);

            return inserted;
        }
    }
}
