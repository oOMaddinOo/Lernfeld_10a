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
