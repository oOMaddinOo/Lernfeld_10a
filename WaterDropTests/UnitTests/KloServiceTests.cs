using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using WaterDrop.Components.Data;
using WaterDrop.Components.Models;
using WaterDrop.Components.Services;

namespace WaterDropTests.UnitTests
{
	[Trait("Category", "Unit")]
	public class KloServiceTests : IDisposable
	{
		private readonly ApplicationDbContext _context;
		private readonly kloService _service;
		private readonly IMemoryCache _cache;
		private readonly Mock<ILogger<kloService>> _mockLogger;
		private readonly Mock<IGeocodingService> _mockGeocodingService;

		public KloServiceTests()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.Options;
			
			_cache = new MemoryCache(new MemoryCacheOptions());
			_mockLogger = new Mock<ILogger<kloService>>();
			_mockGeocodingService = new Mock<IGeocodingService>();
			
			_context = new ApplicationDbContext(options);
			_service = new kloService(_context, _cache, _mockLogger.Object, _mockGeocodingService.Object);
		}

		[Fact]
		public async Task AddKloCommentToData_ShouldAddKloModelToDatabase()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Test Kommentar", 123456);

			// Act
			await _service.AddKloCommentToData(kloModel);

			// Assert
			var result = await _context.DatabaseKloModel.FindAsync(kloModel.Id);
			Assert.NotNull(result);
			Assert.Equal("Test Kommentar", result.Comment);
			Assert.Equal(123456, result.ElementId);
			Assert.Single(_context.DatabaseKloModel);
		}

		[Fact]
		public async Task GetKloByElementId_ShouldReturnCorrectKloModel()
		{
			// Arrange
			var elementId = 9106108128L;
			var kloModel = CreateTestKloModel("Test Kommentar", elementId);
			await _service.AddKloCommentToData(kloModel);

			// Act
			var result = await _service.GetKloByElementId(elementId);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(elementId, result.ElementId);
			Assert.Equal("Test Kommentar", result.Comment);
		}

		[Fact]
		public async Task GetKloByElementId_WithNonExistentElementId_ShouldReturnNull()
		{
			// Arrange
			var nonExistentElementId = 999999999L;

			// Act
			var result = await _service.GetKloByElementId(nonExistentElementId);

			// Assert
			Assert.Null(result);
		}

		[Fact]
		public async Task GetKlosByElementIds_ShouldReturnMatchingKloModels()
		{
			// Arrange
			var klo1 = CreateTestKloModel("Kommentar 1", 111111);
			var klo2 = CreateTestKloModel("Kommentar 2", 222222);
			var klo3 = CreateTestKloModel("Kommentar 3", 333333);
			
			await _service.AddKloCommentToData(klo1);
			await _service.AddKloCommentToData(klo2);
			await _service.AddKloCommentToData(klo3);

			// Act
			var result = await _service.GetKlosByElementIds(new[] { 111111L, 333333L });

			// Assert
			Assert.NotNull(result);
			Assert.Equal(2, result.Count);
			Assert.Contains(result, k => k.ElementId == 111111);
			Assert.Contains(result, k => k.ElementId == 333333);
			Assert.DoesNotContain(result, k => k.ElementId == 222222);
		}

		[Fact]
		public async Task GetKlosByElementIds_WithEmptyArray_ShouldReturnEmptyList()
		{
			// Arrange
			var klo1 = CreateTestKloModel("Kommentar 1", 111111);
			await _service.AddKloCommentToData(klo1);

			// Act
			var result = await _service.GetKlosByElementIds(Array.Empty<long>());

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
		}

		[Fact]
		public async Task UpdateCommentData_ShouldUpdateExistingKloModel()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Original Kommentar", 444444);
			await _service.AddKloCommentToData(kloModel);

			// Detach to simulate a fresh context
			_context.Entry(kloModel).State = EntityState.Detached;

			var updatedKlo = await _context.DatabaseKloModel
				.FirstAsync(k => k.Id == kloModel.Id);
			updatedKlo.Comment = "Aktualisierter Kommentar";

			// Act
			await _service.UpdateCommentData(updatedKlo);

			// Assert
			var result = await _context.DatabaseKloModel.FindAsync(kloModel.Id);
			Assert.NotNull(result);
			Assert.Equal("Aktualisierter Kommentar", result.Comment);
		}

		[Fact]
		public async Task GetToiletsByCity_ShouldReturnToiletsFromDatabase()
		{
			// Arrange
			var city = "Hamburg";
			var bbox = new BoundingBox
			{
				MinLat = 53.395,
				MaxLat = 53.745,
				MinLon = 9.731,
				MaxLon = 10.325,
				DisplayName = "Hamburg, Deutschland"
			};

			_mockGeocodingService
				.Setup(x => x.GetCityBoundingBoxAsync(city))
				.ReturnsAsync(bbox);

			// Toiletten in Hamburg hinzufügen
			_context.Toilets.AddRange(
				new ToiletData { Id = Guid.NewGuid(), ElementId = 1, Lat = 53.5, Lon = 10.0, Type = "node", Tags = new() },
				new ToiletData { Id = Guid.NewGuid(), ElementId = 2, Lat = 53.6, Lon = 10.1, Type = "node", Tags = new() },
				new ToiletData { Id = Guid.NewGuid(), ElementId = 3, Lat = 50.0, Lon = 8.0, Type = "node", Tags = new() } // Außerhalb Hamburg
			);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetToiletsByCity(city);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(2, result.Elements.Count);
			Assert.All(result.Elements, e =>
			{
				Assert.NotNull(e.Lat);
				Assert.NotNull(e.Lon);
				Assert.InRange(e.Lat.Value, bbox.MinLat, bbox.MaxLat);
				Assert.InRange(e.Lon.Value, bbox.MinLon, bbox.MaxLon);
			});
		}

		[Fact]
		public async Task GetToiletsByCity_WithNullOrEmptyCity_ShouldUseHamburgAsDefault()
		{
			// Arrange
			var defaultBbox = new BoundingBox
			{
				MinLat = 53.395,
				MaxLat = 53.745,
				MinLon = 9.731,
				MaxLon = 10.325,
				DisplayName = "Hamburg, Deutschland"
			};

			_mockGeocodingService
				.Setup(x => x.GetCityBoundingBoxAsync("Hamburg"))
				.ReturnsAsync(defaultBbox);

			// Act
			var result = await _service.GetToiletsByCity(null);

			// Assert
			Assert.NotNull(result);
			_mockGeocodingService.Verify(x => x.GetCityBoundingBoxAsync("Hamburg"), Times.Once);
		}

		[Fact]
		public async Task GetToiletsByCity_ShouldUseCacheOnSecondCall()
		{
			// Arrange
			var city = "Berlin";
			var bbox = new BoundingBox
			{
				MinLat = 52.338,
				MaxLat = 52.676,
				MinLon = 13.088,
				MaxLon = 13.761,
				DisplayName = "Berlin, Deutschland"
			};

			_mockGeocodingService
				.Setup(x => x.GetCityBoundingBoxAsync(city))
				.ReturnsAsync(bbox);

			// Act
			var result1 = await _service.GetToiletsByCity(city);
			var result2 = await _service.GetToiletsByCity(city);

			// Assert
			Assert.NotNull(result1);
			Assert.NotNull(result2);
			Assert.Same(result1, result2); // Sollte dasselbe gecachte Objekt sein
			_mockGeocodingService.Verify(x => x.GetCityBoundingBoxAsync(city), Times.Once); // Nur einmal aufgerufen
		}

		// ===== Regressionstest: drinking_water-Filter zeigt 0 =====
		//
		// Vorher hat `GetToiletsByCity` direkt projiziert
		// (`.Select(t => new Element { Tags = t.Tags })`). Weil `Tags`
		// `[NotMapped]` ist und der Getter intern `TagsJson` liest, konnte
		// EF Core die TagsJson-Spalte aus dem SELECT wegoptimieren — dann
		// kam Tags im Element leer zurück und das JS-Frontend fand keine
		// amenity=drinking_water-Datensätze. Dieser Test stellt sicher,
		// dass das Tags-Dictionary tatsächlich gefüllt durch die Service-
		// Schicht durchkommt.
		[Fact]
		public async Task GetToiletsByCity_ShouldPreserveAmenityTagOnElements()
		{
			// Arrange
			var city = "Hamburg";
			var bbox = new BoundingBox
			{
				MinLat = 53.395, MaxLat = 53.745,
				MinLon = 9.731,  MaxLon = 10.325,
				DisplayName = "Hamburg, Deutschland"
			};

			_mockGeocodingService
				.Setup(x => x.GetCityBoundingBoxAsync(city))
				.ReturnsAsync(bbox);

			// Eine Toilette + ein Trinkbrunnen, beide in Hamburg.
			// Wichtig: Tags als Dictionary setzen — der ToiletData-Setter
			// serialisiert das automatisch nach TagsJson (so wie der echte
			// Seed in seed-drinking-water-hamburg.sql).
			_context.Toilets.AddRange(
				new ToiletData
				{
					Id = Guid.NewGuid(), ElementId = 10001,
					Lat = 53.55, Lon = 10.0, Type = "node",
					Tags = new Dictionary<string, string> { ["amenity"] = "toilets" }
				},
				new ToiletData
				{
					Id = Guid.NewGuid(), ElementId = 10002,
					Lat = 53.60, Lon = 10.0, Type = "node",
					Tags = new Dictionary<string, string>
					{
						["amenity"] = "drinking_water",
						["name"]    = "Stadtpark Trinkbrunnen"
					}
				}
			);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetToiletsByCity(city);

			// Assert — beide Datensätze sind da UND amenity ist erhalten
			Assert.Equal(2, result.Elements.Count);

			var toilet = result.Elements.Single(e => e.ElementId == 10001);
			Assert.NotNull(toilet.Tags);
			Assert.Equal("toilets", toilet.Tags["amenity"]);

			var water = result.Elements.Single(e => e.ElementId == 10002);
			Assert.NotNull(water.Tags);
			Assert.Equal("drinking_water", water.Tags["amenity"]);
			Assert.Equal("Stadtpark Trinkbrunnen", water.Tags["name"]);
		}

		// ===== Tests für die neue "Mehrere Reviews pro Ort"-Logik =====

		[Fact]
		public async Task AddKloCommentToData_WhenCreatedAtNotSet_ShouldStampUtcNow()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Stempel-Test", 700001);
			Assert.Null(kloModel.CreatedAt); // sicherstellen, dass kein Vorwert da ist

			var beforeInsert = DateTime.UtcNow.AddSeconds(-1);

			// Act
			await _service.AddKloCommentToData(kloModel);

			// Assert
			var afterInsert = DateTime.UtcNow.AddSeconds(1);
			var saved = await _context.DatabaseKloModel.FindAsync(kloModel.Id);
			Assert.NotNull(saved);
			Assert.NotNull(saved.CreatedAt);
			Assert.InRange(saved.CreatedAt!.Value, beforeInsert, afterInsert);
		}

		[Fact]
		public async Task AddKloCommentToData_WhenCreatedAtAlreadySet_ShouldNotOverwrite()
		{
			// Arrange
			var fixedTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
			var kloModel = CreateTestKloModel("Vorgegebenes Datum", 700002);
			kloModel.CreatedAt = fixedTime;

			// Act
			await _service.AddKloCommentToData(kloModel);

			// Assert
			var saved = await _context.DatabaseKloModel.FindAsync(kloModel.Id);
			Assert.Equal(fixedTime, saved!.CreatedAt);
		}

		[Fact]
		public async Task GetKloByElementId_WithMultipleReviews_ShouldReturnLatestByCreatedAt()
		{
			// Arrange — drei Reviews für denselben Ort, mit aufsteigenden Zeitstempeln
			var elementId = 700003L;
			var older = CreateTestKloModel("Alt", elementId);
			older.CreatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

			var middle = CreateTestKloModel("Mittel", elementId);
			middle.CreatedAt = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);

			var newest = CreateTestKloModel("Neu", elementId);
			newest.CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

			// In zufälliger Reihenfolge einfügen, damit Ordering wirklich getestet wird
			await _service.AddKloCommentToData(middle);
			await _service.AddKloCommentToData(newest);
			await _service.AddKloCommentToData(older);

			// Act
			var result = await _service.GetKloByElementId(elementId);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("Neu", result.Comment);
		}

		[Fact]
		public async Task GetAllKlosByElementId_ShouldReturnAllReviews_OrderedByCreatedAtDesc()
		{
			// Arrange
			var elementId = 700004L;
			var r1 = CreateTestKloModel("Erster", elementId);
			r1.CreatedAt = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc);
			var r2 = CreateTestKloModel("Zweiter", elementId);
			r2.CreatedAt = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);
			var r3 = CreateTestKloModel("Dritter", elementId);
			r3.CreatedAt = new DateTime(2026, 2, 1, 11, 0, 0, DateTimeKind.Utc);

			// Ein Review für einen ANDEREN Ort — der darf nicht mit zurückkommen
			var otherPlace = CreateTestKloModel("Anderer Ort", 700005L);

			await _service.AddKloCommentToData(r1);
			await _service.AddKloCommentToData(r3);
			await _service.AddKloCommentToData(r2);
			await _service.AddKloCommentToData(otherPlace);

			// Act
			var result = await _service.GetAllKlosByElementId(elementId);

			// Assert
			Assert.Equal(3, result.Count);
			Assert.Equal("Dritter", result[0].Comment);  // neueste zuerst
			Assert.Equal("Zweiter", result[1].Comment);
			Assert.Equal("Erster",  result[2].Comment);
		}

		[Fact]
		public async Task GetAllKlosByElementId_WhenNoReviewsExist_ShouldReturnEmptyList()
		{
			// Act
			var result = await _service.GetAllKlosByElementId(999999L);

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
		}

		private DatabaseKloModel CreateTestKloModel(string comment, long elementId)
		{
			return new DatabaseKloModel
			{
				Id = Guid.NewGuid(),
				Comment = comment,
				PictureUrl = "https://example.com/new-picture.jpg",
				ElementId = elementId
			};
		}

		public void Dispose()
		{
			_context.Database.EnsureDeleted();
			_context.Dispose();
			_cache.Dispose();
		}
	}
}
