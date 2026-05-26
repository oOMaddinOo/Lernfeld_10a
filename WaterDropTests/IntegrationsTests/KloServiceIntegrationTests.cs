using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WaterDrop.Components.Data;
using WaterDrop.Components.Models;
using WaterDrop.Components.Services;

namespace WaterDropTests.IntegrationsTests
{
	[Trait("Category", "Integration")]
	public class KloServiceIntegrationTests : IDisposable
	{
		private readonly ApplicationDbContext _context;
		private readonly kloService _service;
		private readonly Mock<ILogger<kloService>> _mockLogger;
		private readonly Mock<IGeocodingService> _mockGeocodingService;

		public KloServiceIntegrationTests()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.Options;

			_context = new ApplicationDbContext(options);
			_mockLogger = new Mock<ILogger<kloService>>();
			_mockGeocodingService = new Mock<IGeocodingService>();
			_service = new kloService(_context, _mockLogger.Object, _mockGeocodingService.Object);
		}

		[Fact]
		public async Task AddKloCommentToData_ShouldPersistToDatabase()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Integration Test", 123456);

			// Act
			await _service.AddKloCommentToData(kloModel);

			// Assert
			var result = await _context.DatabaseKloModel.FindAsync(kloModel.Id);
			Assert.NotNull(result);
			Assert.Equal("Integration Test", result.Comment);
			Assert.Equal(123456, result.ElementId);
			Assert.Single(_context.DatabaseKloModel);
		}

		[Fact]
		public async Task AddMultipleKloModels_ShouldPersistCorrectly()
		{
			// Arrange
			var klo1 = CreateTestKloModel("Hamburg Hauptbahnhof", 111111);
			var klo2 = CreateTestKloModel("Berlin Alexanderplatz", 222222);
			var klo3 = CreateTestKloModel("München Marienplatz", 333333);

			// Act
			await _service.AddKloCommentToData(klo1);
			await _service.AddKloCommentToData(klo2);
			await _service.AddKloCommentToData(klo3);

			// Assert
			var allKlos = await _context.DatabaseKloModel.ToListAsync();
			Assert.Equal(3, allKlos.Count);

			// Verify all have correct data
			Assert.All(allKlos, klo =>
			{
				Assert.NotNull(klo.Comment);
				Assert.NotEqual(0, klo.ElementId);
			});
		}

		[Fact]
		public async Task UpdateCommentData_WithChangedProperties_ShouldPersistChanges()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Original", 444444);
			await _service.AddKloCommentToData(kloModel);

			// Detach to simulate a fresh context
			_context.Entry(kloModel).State = EntityState.Detached;

			// Act - Load and modify
			var loadedKlo = await _context.DatabaseKloModel.FirstAsync(k => k.Id == kloModel.Id);
			loadedKlo.Comment = "Modified Comment";
			loadedKlo.PictureUrl = "https://example.com/new-picture.jpg";

			await _service.UpdateCommentData(loadedKlo);

			// Assert
			var updatedKlo = await _context.DatabaseKloModel.FindAsync(kloModel.Id);
			Assert.NotNull(updatedKlo);
			Assert.Equal("Modified Comment", updatedKlo.Comment);
			Assert.Equal("https://example.com/new-picture.jpg", updatedKlo.PictureUrl);
			Assert.Equal(444444, updatedKlo.ElementId);
		}

		[Fact]
		public async Task ConcurrentOperations_ShouldHandleMultipleAdds()
		{
			// Arrange
			var klos = Enumerable.Range(1, 10)
				.Select(i => CreateTestKloModel($"Klo {i}", 1000000 + i))
				.ToList();

			// Act - Simulate concurrent adds
			var tasks = klos.Select(klo => _service.AddKloCommentToData(klo));
			await Task.WhenAll(tasks);

			// Assert
			var allKlos = await _context.DatabaseKloModel.ToListAsync();
			Assert.Equal(10, allKlos.Count);
			Assert.Equal(10, allKlos.Select(k => k.ElementId).Distinct().Count());
		}


		[Fact]
		public async Task GetOneKloData_AfterMultipleUpdates_ShouldReturnLatestVersion()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Version 1", 666666);
			await _service.AddKloCommentToData(kloModel);

			// Act - Multiple updates
			for (int i = 2; i <= 5; i++)
			{
				_context.Entry(kloModel).State = EntityState.Detached;
				var klo = await _context.DatabaseKloModel.FirstAsync(k => k.Id == kloModel.Id);
				klo.Comment = $"Version {i}";
				await _service.UpdateCommentData(klo);
			}

			// Assert
			var finalKlo = await _context.DatabaseKloModel.FindAsync(kloModel.Id);
			Assert.NotNull(finalKlo);
			Assert.Equal("Version 5", finalKlo.Comment);
		}

		[Fact]
		public async Task GetKloByElementId_ShouldReturnCorrectKloModel()
		{
			// Arrange
			var elementId = 9106108128L;
			var kloModel = CreateTestKloModel("Test by ElementId", elementId);
			await _service.AddKloCommentToData(kloModel);

			// Act
			var result = await _service.GetKloByElementId(elementId);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(elementId, result.ElementId);
			Assert.Equal("Test by ElementId", result.Comment);
		}

		[Fact]
		public async Task GetKloByElementId_WithMultipleKlos_ShouldReturnCorrectOne()
		{
			// Arrange
			var klo1 = CreateTestKloModel("Klo 1", 777777);
			var klo2 = CreateTestKloModel("Klo 2", 888888);
			var klo3 = CreateTestKloModel("Klo 3", 999999);

			await _service.AddKloCommentToData(klo1);
			await _service.AddKloCommentToData(klo2);
			await _service.AddKloCommentToData(klo3);

			// Act
			var result = await _service.GetKloByElementId(888888);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(888888, result.ElementId);
			Assert.Equal("Klo 2", result.Comment);
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
		public async Task GetKlosByElementIds_WithLargeDataset_ShouldReturnOnlyRequestedIds()
		{
			// Arrange
			for (int i = 1; i <= 100; i++)
			{
				var klo = CreateTestKloModel($"Klo {i}", 1000 + i);
				await _service.AddKloCommentToData(klo);
			}

			var requestedIds = new[] { 1010L, 1025L, 1050L, 1075L, 1090L };

			// Act
			var result = await _service.GetKlosByElementIds(requestedIds);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(5, result.Count);
			Assert.All(requestedIds, id => Assert.Contains(result, k => k.ElementId == id));
		}

		[Fact]
		public async Task UpdateKloModel_ShouldNotChangeElementId()
		{
			// Arrange
			var originalElementId = 123123123L;
			var kloModel = CreateTestKloModel("Original", originalElementId);
			await _service.AddKloCommentToData(kloModel);

			// Detach to simulate a fresh context
			_context.Entry(kloModel).State = EntityState.Detached;

			// Act
			var loadedKlo = await _context.DatabaseKloModel.FirstAsync(k => k.Id == kloModel.Id);
			loadedKlo.Comment = "Updated";
			loadedKlo.PictureUrl = "https://example.com/updated.jpg";
			await _service.UpdateCommentData(loadedKlo);

			// Assert
			var updatedKlo = await _context.DatabaseKloModel.FindAsync(kloModel.Id);
			Assert.NotNull(updatedKlo);
			Assert.Equal(originalElementId, updatedKlo.ElementId);
			Assert.Equal("Updated", updatedKlo.Comment);
		}

		[Fact]
		public async Task AddKloModel_WithSameElementId_ShouldAllowDuplicates()
		{
			// Arrange
			var elementId = 456456456L;
			var klo1 = CreateTestKloModel("Comment 1", elementId);
			var klo2 = CreateTestKloModel("Comment 2", elementId);

			// Act
			await _service.AddKloCommentToData(klo1);
			await _service.AddKloCommentToData(klo2);

			// Assert
			var allKlos = await _context.DatabaseKloModel.ToListAsync();
			Assert.Equal(2, allKlos.Count);
			Assert.All(allKlos, klo => Assert.Equal(elementId, klo.ElementId));
		}

		[Fact]
		public async Task AddKloModel_WithNullPictureUrl_ShouldPersist()
		{
			// Arrange
			var kloModel = new DatabaseKloModel
			{
				Id = Guid.NewGuid(),
				Comment = "No Picture",
				PictureUrl = null,
				ElementId = 789789789
			};

			// Act
			await _service.AddKloCommentToData(kloModel);

			// Assert
			var savedKlo = await _context.DatabaseKloModel.FindAsync(kloModel.Id);
			Assert.NotNull(savedKlo);
			Assert.Null(savedKlo.PictureUrl);
			Assert.Equal("No Picture", savedKlo.Comment);
		}

		[Fact]
		public async Task GetToiletsByCity_ShouldReturnToiletsFromDatabaseWithinBoundingBox()
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

			// Toiletten in Hamburg und außerhalb hinzufügen
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

			_context.Toilets.Add(
				new ToiletData { Id = Guid.NewGuid(), ElementId = 1, Lat = 53.5, Lon = 10.0, Type = "node", Tags = new() }
			);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetToiletsByCity(null);

			// Assert
			Assert.NotNull(result);
			_mockGeocodingService.Verify(x => x.GetCityBoundingBoxAsync("Hamburg"), Times.Once);
		}

		[Fact]
		public async Task GetToiletsByCity_WithDifferentCities_ShouldReturnDifferentResults()
		{
			// Arrange
			var hamburgBbox = new BoundingBox
			{
				MinLat = 53.395,
				MaxLat = 53.745,
				MinLon = 9.731,
				MaxLon = 10.325,
				DisplayName = "Hamburg, Deutschland"
			};

			var berlinBbox = new BoundingBox
			{
				MinLat = 52.338,
				MaxLat = 52.676,
				MinLon = 13.088,
				MaxLon = 13.761,
				DisplayName = "Berlin, Deutschland"
			};

			_mockGeocodingService
				.Setup(x => x.GetCityBoundingBoxAsync("Hamburg"))
				.ReturnsAsync(hamburgBbox);

			_mockGeocodingService
				.Setup(x => x.GetCityBoundingBoxAsync("Berlin"))
				.ReturnsAsync(berlinBbox);

			// Toiletten in verschiedenen Städten hinzufügen
			_context.Toilets.AddRange(
				new ToiletData { Id = Guid.NewGuid(), ElementId = 1, Lat = 53.5, Lon = 10.0, Type = "node", Tags = new() }, // Hamburg
				new ToiletData { Id = Guid.NewGuid(), ElementId = 2, Lat = 52.5, Lon = 13.4, Type = "node", Tags = new() }  // Berlin
			);
			await _context.SaveChangesAsync();

			// Act
			var hamburgResult = await _service.GetToiletsByCity("Hamburg");
			var berlinResult = await _service.GetToiletsByCity("Berlin");

			// Assert
			Assert.NotNull(hamburgResult);
			Assert.NotNull(berlinResult);
			Assert.Single(hamburgResult.Elements);
			Assert.Single(berlinResult.Elements);
			Assert.NotSame(hamburgResult, berlinResult);
		}

		[Fact]
		public async Task GetToiletsByCity_WithLargeDataset_ShouldFilterCorrectly()
		{
			// Arrange
			var city = "München";
			var bbox = new BoundingBox
			{
				MinLat = 48.061,
				MaxLat = 48.248,
				MinLon = 11.360,
				MaxLon = 11.723,
				DisplayName = "München, Deutschland"
			};

			_mockGeocodingService
				.Setup(x => x.GetCityBoundingBoxAsync(city))
				.ReturnsAsync(bbox);

			// 100 Toiletten hinzufügen - 50 innerhalb, 50 außerhalb
			var toilets = new List<ToiletData>();
			for (int i = 0; i < 50; i++)
			{
				toilets.Add(new ToiletData
				{
					Id = Guid.NewGuid(),
					ElementId = i,
					Lat = 48.1 + (i * 0.001),
					Lon = 11.5 + (i * 0.001),
					Type = "node",
					Tags = new()
				}); // Innerhalb
			}
			for (int i = 50; i < 100; i++)
			{
				toilets.Add(new ToiletData
				{
					Id = Guid.NewGuid(),
					ElementId = i,
					Lat = 50.0 + (i * 0.001),
					Lon = 8.0 + (i * 0.001),
					Type = "node",
					Tags = new()
				}); // Außerhalb
			}
			_context.Toilets.AddRange(toilets);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetToiletsByCity(city);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(50, result.Elements.Count);
			Assert.All(result.Elements, e =>
			{
				Assert.NotNull(e.Lat);
				Assert.NotNull(e.Lon);
				Assert.InRange(e.Lat.Value, bbox.MinLat, bbox.MaxLat);
				Assert.InRange(e.Lon.Value, bbox.MinLon, bbox.MaxLon);
			});
		}

		[Fact]
		public async Task GetToiletsByCity_WithNullBoundingBox_ShouldUseDefaultHamburgBbox()
		{
			// Arrange
			var city = "UnknownCity";

			_mockGeocodingService
				.Setup(x => x.GetCityBoundingBoxAsync(city))
				.ReturnsAsync((BoundingBox)null);

			_context.Toilets.AddRange(
				new ToiletData { Id = Guid.NewGuid(), ElementId = 1, Lat = 53.5, Lon = 10.0, Type = "node", Tags = new() }, // In Hamburg Default bbox
				new ToiletData { Id = Guid.NewGuid(), ElementId = 2, Lat = 50.0, Lon = 8.0, Type = "node", Tags = new() }   // Außerhalb
			);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetToiletsByCity(city);

			// Assert
			Assert.NotNull(result);
			Assert.Single(result.Elements); // Nur die Toilette innerhalb der Default-Hamburg-BoundingBox
			Assert.Equal(1, result.Elements[0].ElementId);
		}

		private DatabaseKloModel CreateTestKloModel(string comment, long elementId)
		{
			return new DatabaseKloModel
			{
				Id = Guid.NewGuid(),
				Comment = comment,
				PictureUrl = "https://waterdropstorage.blob.core.windows.net/picture/golden-toilet.jpg",
				ElementId = elementId
			};
		}

		public void Dispose()
		{
			_context?.Dispose();
		}
	}
}