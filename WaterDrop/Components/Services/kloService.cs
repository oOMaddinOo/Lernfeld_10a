using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WaterDrop.Components.Data;
using WaterDrop.Components.Models;

namespace WaterDrop.Components.Services
{
	public class kloService
	{
		private readonly ApplicationDbContext _context;
		private readonly IMemoryCache _cache;
		private readonly ILogger<kloService> _logger;
		private readonly IGeocodingService _geocodingService;

		public kloService(
			ApplicationDbContext context,
			IMemoryCache cache,
			ILogger<kloService> logger,
			IGeocodingService geocodingService)
		{
			_context = context;
			_cache = cache;
			_logger = logger;
			_geocodingService = geocodingService;
		}

		public async Task<KloModel> GetToiletsByCity(string city)
		{
			_logger.LogInformation("GetToiletsByCity aufgerufen mit: '{City}'", city);

			if (string.IsNullOrWhiteSpace(city))
			{
				city = "Hamburg";
				_logger.LogWarning("Keine Stadt angegeben - nutze Default: Hamburg");
			}

			var cacheKey = $"toilets_{city}";

			_logger.LogInformation("Cache-Key: {CacheKey}", cacheKey);

			if (_cache.TryGetValue<KloModel>(cacheKey, out var cachedResult))
			{
				_logger.LogInformation("Cache HIT für {City} - {Count} Toiletten", city, cachedResult.Elements.Count);
				return cachedResult;
			}

			_logger.LogInformation("Cache MISS - lade Daten für {City}", city);

			var bbox = await _geocodingService.GetCityBoundingBoxAsync(city);

			if (bbox == null)
			{
				_logger.LogWarning("Keine Bounding Box für {City} gefunden - nutze Default (Hamburg)", city);
				bbox = new BoundingBox
				{
					MinLat = 53.395,
					MaxLat = 53.745,
					MinLon = 9.731,
					MaxLon = 10.325,
					DisplayName = "Hamburg, Deutschland (Default)"
				};
			}

			_logger.LogInformation("Bounding Box für {City}: {BBox}", city, bbox);

			var toilets = await _context.Toilets
				.Where(t => t.Lat >= bbox.MinLat && t.Lat <= bbox.MaxLat &&
				            t.Lon >= bbox.MinLon && t.Lon <= bbox.MaxLon)
				.Select(t => new Element
				{
					Id = t.Id,
					ElementId = t.ElementId,
					Lat = t.Lat,
					Lon = t.Lon,
					Type = t.Type,
					Tags = t.Tags
				})
				.ToListAsync();

			_logger.LogInformation("{Count} Toiletten in {City} ({DisplayName}) gefunden",
				toilets.Count, city, bbox.DisplayName);

			var result = new KloModel
			{
				Elements = toilets
			};

			_cache.Set(cacheKey, result, TimeSpan.FromDays(7));
			_logger.LogInformation("Ergebnis für {City} gecacht", city);

			return result;
		}

		public async Task<KloModel> GetToilets(ToiletQueryBuilder queryBuilder)
		{
			var query = queryBuilder.Build();
			var city = ExtractCityFromQuery(query);
			_logger.LogInformation("GetToilets (via QueryBuilder) - extrahierte Stadt: '{City}'", city);
			return await GetToiletsByCity(city);
		}

		private string ExtractCityFromQuery(string query)
		{
			var patterns = new[]
			{
				@"area\[""name""\]\s*=\s*""([^""]+)""",
				@"area\[name\]\s*=\s*""([^""]+)""",
				@"""name""\s*=\s*""([^""]+)"""
			};

			foreach (var pattern in patterns)
			{
				var match = System.Text.RegularExpressions.Regex.Match(
					query,
					pattern,
					System.Text.RegularExpressions.RegexOptions.IgnoreCase
				);

				if (match.Success)
				{
					var city = match.Groups[1].Value;
					_logger.LogInformation("Stadt extrahiert (Pattern: {Pattern}): '{City}'", pattern, city);
					return city;
				}
			}

			_logger.LogWarning("Keine Stadt im Query gefunden - nutze Default 'Hamburg'");
			return "Hamburg";
		}

		public async Task<DatabaseKloModel?> GetKloByElementId(long elementId)
		{
			return await _context.DatabaseKloModel
				.Where(k => k.ElementId == elementId)
				.OrderByDescending(k => k.CreatedAt ?? DateTime.MinValue)
				.ThenByDescending(k => k.Id)
				.FirstOrDefaultAsync();
		}

		public async Task<List<DatabaseKloModel>> GetAllKlosByElementId(long elementId)
		{
			return await _context.DatabaseKloModel
				.Where(k => k.ElementId == elementId)
				.OrderByDescending(k => k.CreatedAt ?? DateTime.MinValue)
				.ThenByDescending(k => k.Id)
				.ToListAsync();
		}

		public async Task<List<DatabaseKloModel>> GetKlosByElementIds(long[] elementIds)
		{
			if (elementIds.Length == 0)
			{
				return new List<DatabaseKloModel>();
			}

			var reviews = await _context.DatabaseKloModel
				.Where(k => elementIds.Contains(k.ElementId))
				.OrderByDescending(k => k.CreatedAt ?? DateTime.MinValue)
				.ThenByDescending(k => k.Id)
				.ToListAsync();

			return reviews
				.GroupBy(k => k.ElementId)
				.Select(group => group.First())
				.ToList();
		}

		public async Task AddKloCommentToData(DatabaseKloModel klo)
		{
			klo.CreatedAt ??= DateTime.UtcNow;
			_context.DatabaseKloModel.Add(klo);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateCommentData(DatabaseKloModel klo)
		{
			_context.DatabaseKloModel.Update(klo);
			await _context.SaveChangesAsync();
		}
	}
}
