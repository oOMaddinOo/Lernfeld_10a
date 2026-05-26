using Microsoft.EntityFrameworkCore;
using WaterDrop.Components.Data;
using WaterDrop.Components.Models;

namespace WaterDrop.Components.Services
{
	public class kloService
	{
		private readonly ApplicationDbContext _context;
		private readonly ILogger<kloService> _logger;
		private readonly IGeocodingService _geocodingService;

		public kloService(
			ApplicationDbContext context,
			ILogger<kloService> logger,
			IGeocodingService geocodingService)
		{
			_context = context;
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
			var rawToilets = await _context.Toilets
				.Where(t => t.Lat >= bbox.MinLat && t.Lat <= bbox.MaxLat &&
							t.Lon >= bbox.MinLon && t.Lon <= bbox.MaxLon)
				.AsNoTracking()
				.ToListAsync();

			var toilets = rawToilets
				.Select(t => new Element
				{
					Id = t.Id,
					ElementId = t.ElementId,
					Lat = t.Lat,
					Lon = t.Lon,
					Type = t.Type,
					Tags = t.Tags ?? new Dictionary<string, string>()
				})
				.ToList();
			var byAmenity = toilets
				.GroupBy(e => e.Tags != null && e.Tags.TryGetValue("amenity", out var a) ? a : "(none)")
				.ToDictionary(g => g.Key, g => g.Count());

			_logger.LogInformation(
				"{Total} Datensätze in {City} ({DisplayName}) gefunden. Aufschlüsselung nach amenity: {Breakdown}",
				toilets.Count, city, bbox.DisplayName,
				string.Join(", ", byAmenity.Select(kv => $"{kv.Key}={kv.Value}"))
			);

			var result = new KloModel
			{
				Elements = toilets
			};
			return result;
		}

		/// <summary>
		/// Diagnose-Methode: zählt direkt aus der DB (kein Cache, kein
		/// Bounding-Box-Filter), wie viele Datensätze welche amenity haben
		/// und ob speziell drinking_water-Reihen für Hamburg existieren.
		/// Gibt das Ergebnis als String zurück, der direkt im UI angezeigt
		/// werden kann — so muss niemand Server-Logs lesen, um zu prüfen,
		/// ob der Seed-Insert in der richtigen DB gelandet ist.
		/// </summary>
		public async Task<string> GetDebugStatsAsync()
		{
			// 0) WICHTIG: Server- und Datenbankname direkt von SQL Server
			//    erfragen. So sehen wir schwarz auf weiß, gegen welche DB
			//    die App gerade arbeitet — und können das mit dem Query-
			//    Editor vergleichen, in dem der Seed gelaufen ist.
			//    Wenn diese beiden Quellen unterschiedliche DB-Namen
			//    zeigen, ist das Problem gelöst: einfach den Seed auf
			//    der richtigen DB einspielen.
			string serverName = "?", dbName = "?";
			try
			{
				var conn = _context.Database.GetDbConnection();
				if (conn.State != System.Data.ConnectionState.Open)
					await conn.OpenAsync();

				await using var cmd = conn.CreateCommand();
				cmd.CommandText = "SELECT CAST(@@SERVERNAME AS NVARCHAR(128)) + '|' + DB_NAME()";
				var raw = (await cmd.ExecuteScalarAsync())?.ToString() ?? "?|?";
				var parts = raw.Split('|', 2);
				serverName = parts.Length > 0 ? parts[0] : "?";
				dbName = parts.Length > 1 ? parts[1] : "?";
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Konnte SERVERNAME/DB_NAME nicht ermitteln");
			}

			// 1) Gesamt-Zeilenzahl in der Toilets-Tabelle.
			var total = await _context.Toilets.CountAsync();

			// 2) Anzahl der Zeilen, deren TagsJson "amenity":"drinking_water"
			//    enthält. Wir verwenden hier denselben LIKE-Pattern wie das
			//    Verifikations-Statement am Ende des Seed-Scripts, mittels
			//    `EF.Functions.Like` — kein doppeltes Escape durch EF Core.
			//    So sind die beiden Zahlen direkt vergleichbar.
			const string dwLike = "%\"amenity\":\"drinking_water\"%";
			var dwTotal = await _context.Toilets
				.Where(t => t.TagsJson != null && EF.Functions.Like(t.TagsJson, dwLike))
				.CountAsync();

			// 3) Dasselbe, aber zusätzlich auf die Hamburg-Bounding-Box
			//    gefiltert — damit wir sehen, ob die Reihen zwar da sind,
			//    aber außerhalb der Hamburg-Koordinaten liegen.
			var dwInHamburg = await _context.Toilets
				.Where(t => t.TagsJson != null && EF.Functions.Like(t.TagsJson, dwLike)
						 && t.Lat >= 53.395 && t.Lat <= 53.745
						 && t.Lon >= 9.731 && t.Lon <= 10.325)
				.CountAsync();

			// 4) Beispiel-ElementIds, damit man im UI sieht, ob es genau die
			//    Seed-Reihen (9000000001..9000000010) sind oder andere
			//    drinking_water-Reihen (z. B. aus einem früheren Overpass-
			//    Import).
			var sampleIds = await _context.Toilets
				.Where(t => t.TagsJson != null && EF.Functions.Like(t.TagsJson, dwLike))
				.OrderBy(t => t.ElementId)
				.Select(t => t.ElementId)
				.Take(5)
				.ToListAsync();

			_logger.LogInformation(
				"DEBUG STATS: server={Server} db={Db} total={Total} dwTotal={DwTotal} dwInHamburg={DwInHamburg} sampleIds=[{Ids}]",
				serverName, dbName, total, dwTotal, dwInHamburg, string.Join(",", sampleIds));

			return
				$"Server: {serverName}\n" +
				$"Database: {dbName}\n" +
				$"Toilets total: {total}\n" +
				$"drinking_water gesamt: {dwTotal}\n" +
				$"drinking_water in Hamburg-BBox: {dwInHamburg}\n" +
				$"Beispiel-IDs: [{string.Join(", ", sampleIds)}]";
		}

		/// <summary>
		/// Gibt den NEUESTEN Review für eine ElementId zurück. Mehrere Reviews
		/// pro Ort sind erlaubt — wir sortieren absteigend nach CreatedAt
		/// (Legacy-Zeilen ohne CreatedAt landen unten) und nehmen den ersten.
		/// Wird vom Karten-Popup verwendet, wo nur ein Eintrag pro Marker erscheint.
		/// </summary>
		public async Task<DatabaseKloModel?> GetKloByElementId(long elementId)
		{
			return await _context.DatabaseKloModel
				.Where(k => k.ElementId == elementId)
				.OrderByDescending(k => k.CreatedAt)
				.ThenByDescending(k => k.Id)
				.FirstOrDefaultAsync();
		}

		public async Task<List<DatabaseKloModel>> GetKlosByElementIds(long[] elementIds)
		{
			return await _context.DatabaseKloModel
				.Where(k => elementIds.Contains(k.ElementId))
				.ToListAsync();
		}

		/// <summary>
		/// Gibt ALLE Reviews für einen Ort zurück, sortiert nach Erstellungs-
		/// zeitpunkt absteigend (neuester zuerst). Wird vom "Mehr Reviews"-Panel
		/// in Home.razor verwendet, um die Historie pro Toilette anzuzeigen.
		/// </summary>
		public async Task<List<DatabaseKloModel>> GetAllKlosByElementId(long elementId)
		{
			return await _context.DatabaseKloModel
				.Where(k => k.ElementId == elementId)
				.OrderByDescending(k => k.CreatedAt)
				.ThenByDescending(k => k.Id)
				.ToListAsync();
		}

		public async Task AddKloCommentToData(DatabaseKloModel klo)
		{
			// Erstellungszeitpunkt stempeln, falls der Aufrufer keinen angegeben
			// hat. Damit landet jeder neue Review zuverlässig mit einem CreatedAt
			// in der DB und das "Mehr Reviews"-Panel kann nach Datum sortieren.
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