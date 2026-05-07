using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using WaterDrop.Components.Data;
using WaterDrop.Components.Models;

namespace WaterDrop.Components.Services
{
	public class kloService
	{
		private static readonly HttpClient _httpClient = new HttpClient();
		private readonly ApplicationDbContext _context;
		private readonly IMemoryCache _cache;
		
		// Cache bleibt 7 Tage gespeichert
		private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(7);

		// Update im Hintergrund nach 1 Stunde
		private static readonly TimeSpan RefreshThreshold = TimeSpan.FromHours(1);
		private const string DbCacheKey = "all_klo_data";
		private const string DbCacheTimestampKey = "all_klo_data_timestamp";

		public kloService(ApplicationDbContext context, IMemoryCache cache)
		{
			_context = context;
			_cache = cache;
		}

		/// <summary>
		/// Stale-While-Revalidate: Gibt sofort Cache zurück, updated im Hintergrund
		/// </summary>
		public async Task<KloModel> GetToilets(ToiletQueryBuilder queryBuilder)
		{
			var query = queryBuilder.Build();
			var cacheKey = $"toilets_{GetQueryHash(query)}";
			var timestampKey = $"{cacheKey}_timestamp";

			// Versuche aus Cache zu laden
			if (_cache.TryGetValue<KloModel>(cacheKey, out var cachedResult))
			{
				Console.WriteLine($"Cache HIT für Query: {cacheKey}");
				
				// Prüfe ob Update im Hintergrund nötig ist
				if (_cache.TryGetValue<DateTime>(timestampKey, out var lastUpdate))
				{
					var age = DateTime.UtcNow - lastUpdate;
					if (age > RefreshThreshold)
					{
						Console.WriteLine($"Cache ist {age.TotalMinutes:F0} Min alt - starte Background-Update");
						// Fire & Forget - Update im Hintergrund
						_ = Task.Run(async () => await UpdateCacheInBackground(query, cacheKey, timestampKey));
					}
				}

				return cachedResult;
			}

			Console.WriteLine($"Cache MISS für Query: {cacheKey}");
			
			// Kein Cache vorhanden - muss warten
			var result = await ExecuteQuery(query);
			SetCacheWithTimestamp(cacheKey, timestampKey, result);

			return result;
		}

		private async Task UpdateCacheInBackground(string query, string cacheKey, string timestampKey)
		{
			try
			{
				Console.WriteLine($"Background-Update gestartet für {cacheKey}");
				var freshData = await ExecuteQuery(query);
				SetCacheWithTimestamp(cacheKey, timestampKey, freshData);
				Console.WriteLine($"Background-Update abgeschlossen für {cacheKey}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Background-Update fehlgeschlagen: {ex.Message}");
			}
		}

		private void SetCacheWithTimestamp<T>(string cacheKey, string timestampKey, T value)
		{
			var cacheOptions = new MemoryCacheEntryOptions()
				.SetAbsoluteExpiration(CacheDuration)
				.SetPriority(CacheItemPriority.High);

			_cache.Set(cacheKey, value, cacheOptions);
			_cache.Set(timestampKey, DateTime.UtcNow, cacheOptions);
		}

		private string GetQueryHash(string query)
		{
			using var sha256 = System.Security.Cryptography.SHA256.Create();
			var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(query));
			return Convert.ToHexString(hashBytes)[..16];
		}

		private async Task<KloModel> ExecuteQuery(string query)
		{
			var url = $"https://overpass-api.de/api/interpreter?data={Uri.EscapeDataString(query)}";

			try
			{
				var request = new HttpRequestMessage(HttpMethod.Get, url);
				request.Headers.Add("Accept", "application/json");
				request.Headers.Add("User-Agent", "WaterDrop/1.0");

				var response = await _httpClient.SendAsync(request);
				response.EnsureSuccessStatusCode();
				var json = await response.Content.ReadAsStringAsync();

				var result = JsonConvert.DeserializeObject<KloModel>(json);

				if (result != null && result.Elements == null)
				{
					result.Elements = new List<Element>();
				}

				return result;
			}
			catch (HttpRequestException httpEx)
			{
				Console.WriteLine($"HTTP Error fetching toilets: {httpEx.Message}");
				return new KloModel { Elements = new List<Element>() };
			}
			catch (JsonException jsonEx)
			{
				Console.WriteLine($"JSON Deserialization Error: {jsonEx.Message}");
				return new KloModel { Elements = new List<Element>() };
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching toilets: {ex.Message}");
				return new KloModel { Elements = new List<Element>() };
			}
		}

		public async Task AddKloCommentToData(DatabaseKloModel klomodel)
		{
			_context.DatabaseKloModel.Add(klomodel);
			await _context.SaveChangesAsync();
			InvalidateDbCache();
		}

		/// <summary>
		/// Stale-While-Revalidate für DB-Daten
		/// </summary>
		public async Task<List<DatabaseKloModel>> GetAllKloData()
		{
			// Versuche aus Cache zu laden
			if (_cache.TryGetValue<List<DatabaseKloModel>>(DbCacheKey, out var cachedData))
			{
				Console.WriteLine("DB Cache HIT");
				
				// Prüfe ob Update im Hintergrund nötig ist
				if (_cache.TryGetValue<DateTime>(DbCacheTimestampKey, out var lastUpdate))
				{
					var age = DateTime.UtcNow - lastUpdate;
					if (age > RefreshThreshold)
					{
						Console.WriteLine($"DB Cache ist {age.TotalMinutes:F0} Min alt - starte Background-Update");
						_ = Task.Run(async () => await UpdateDbCacheInBackground());
					}
				}

				return cachedData;
			}

			Console.WriteLine("DB Cache MISS - Lade von Datenbank");
			var data = await _context.DatabaseKloModel.ToListAsync();
			SetDbCache(data);

			return data;
		}

		private async Task UpdateDbCacheInBackground()
		{
			try
			{
				Console.WriteLine("DB Background-Update gestartet");
				var freshData = await _context.DatabaseKloModel.ToListAsync();
				SetDbCache(freshData);
				Console.WriteLine("DB Background-Update abgeschlossen");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"DB Background-Update fehlgeschlagen: {ex.Message}");
			}
		}

		private void SetDbCache(List<DatabaseKloModel> data)
		{
			var cacheOptions = new MemoryCacheEntryOptions()
				.SetAbsoluteExpiration(CacheDuration)
				.SetPriority(CacheItemPriority.High);

			_cache.Set(DbCacheKey, data, cacheOptions);
			_cache.Set(DbCacheTimestampKey, DateTime.UtcNow, cacheOptions);
		}

		public async Task<Dictionary<long, DatabaseKloModel>> GetAllKloDataAsDictionary()
		{
			var allData = await GetAllKloData();
			
			var dictionary = allData
				.GroupBy(k => k.ElementId)
				.Select(g => g.OrderByDescending(k => k.Id).First())
				.ToDictionary(k => k.ElementId, k => k);

			var duplicateCount = allData.Count - dictionary.Count;
			if (duplicateCount > 0)
			{
				Console.WriteLine($"WARNUNG: {duplicateCount} Duplikat(e) in der Datenbank gefunden und automatisch bereinigt.");
			}

			return dictionary;
		}

		public async Task<List<DatabaseKloModel>> GetKlosByElementIds(long[] elementIds)
		{
			if (elementIds == null || elementIds.Length == 0)
				return new List<DatabaseKloModel>();

			var dictionary = await GetAllKloDataAsDictionary();
			
			var results = elementIds
				.Where(id => dictionary.ContainsKey(id))
				.Select(id => dictionary[id])
				.ToList();

			Console.WriteLine($"GetKlosByElementIds: {results.Count}/{elementIds.Length} Reviews gefunden");
			return results;
		}

		public async Task<DatabaseKloModel> GetKloByElementId(long elementId)
		{
			var dictionary = await GetAllKloDataAsDictionary();
			return dictionary.TryGetValue(elementId, out var klo) ? klo : null;
		}

		public async Task DeleteKloDataComment(Guid? kloId)
		{
			if (kloId == null)
			{
				throw new ArgumentNullException(nameof(kloId));
			}

			var klo = await _context.DatabaseKloModel.FindAsync(kloId);

			if (klo == null)
			{
				return;
			}

			_context.DatabaseKloModel.Remove(klo);
			await _context.SaveChangesAsync();
			InvalidateDbCache();
		}

		public async Task UpdateCommentData(DatabaseKloModel kloModel)
		{
			_context.DatabaseKloModel.Update(kloModel);
			await _context.SaveChangesAsync();
			InvalidateDbCache();
		}

		public async Task<DatabaseKloModel?> GetOneKloData(Guid kloId)
		{
			return await _context.DatabaseKloModel.FirstOrDefaultAsync(k => k.Id == kloId);
		}

		public async Task<int> RemoveDuplicateElementIds()
		{
			var allData = await _context.DatabaseKloModel.ToListAsync();
			
			var duplicates = allData
				.GroupBy(k => k.ElementId)
				.Where(g => g.Count() > 1)
				.SelectMany(g => g.OrderByDescending(k => k.Id).Skip(1))
				.ToList();

			if (duplicates.Any())
			{
				_context.DatabaseKloModel.RemoveRange(duplicates);
				await _context.SaveChangesAsync();
				InvalidateDbCache();
				
				Console.WriteLine($"{duplicates.Count} Duplikat(e) aus der Datenbank entfernt.");
				return duplicates.Count;
			}

			return 0;
		}

		private void InvalidateDbCache()
		{
			_cache.Remove(DbCacheKey);
			_cache.Remove(DbCacheTimestampKey);
			Console.WriteLine("DB Cache invalidiert");
		}

		public void ClearAllCaches()
		{
			InvalidateDbCache();
			Console.WriteLine("Alle Caches gelöscht");
		}

		/// <summary>
		/// Manuelle Cache-Aktualisierung erzwingen (z.B. für Admin-Button)
		/// </summary>
		public async Task ForceRefreshCache(ToiletQueryBuilder queryBuilder)
		{
			var query = queryBuilder.Build();
			var cacheKey = $"toilets_{GetQueryHash(query)}";
			var timestampKey = $"{cacheKey}_timestamp";

			Console.WriteLine($"🔄 Force Refresh gestartet für {cacheKey}");
			var freshData = await ExecuteQuery(query);
			SetCacheWithTimestamp(cacheKey, timestampKey, freshData);
			Console.WriteLine($"✅ Force Refresh abgeschlossen");
		}
	}
}