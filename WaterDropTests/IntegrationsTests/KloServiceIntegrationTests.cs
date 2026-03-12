using Microsoft.EntityFrameworkCore;
using WaterDrop.Components.Data;
using WaterDrop.Components.Models;
using WaterDrop.Components.Services;
using Xunit;

namespace WaterDropTests.IntegrationsTests
{
	[Trait("Category", "Integration")]
	public class KloServiceIntegrationTests
	{
		private readonly ApplicationDbContext _context;
		private readonly kloService _service;

		public KloServiceIntegrationTests()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.Options;

			_context = new ApplicationDbContext(options);
			_service = new kloService(_context);
		}

		[Fact]
		public async Task FullCrudWorkflow_ShouldWorkEndToEnd()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Integration Test");

			// Act & Assert - Add
			await _service.AddKloCommentToData(kloModel);
			var allData = await _service.GetAllKloData();
			Assert.Single(allData);
			Assert.Equal("Integration Test", allData[0].Comment);

			// Act & Assert - Get One
			var singleKlo = await _service.GetOneKloData(kloModel.Id.Value);
			Assert.NotNull(singleKlo);
			Assert.Equal("Integration Test", singleKlo.Comment);
			Assert.Equal(2, singleKlo.Elements.Count);

			// Act & Assert - Update
			singleKlo.Comment = "Updated Integration Test";
			await _service.UpdateCommentData(singleKlo);
			var updatedKlo = await _service.GetOneKloData(kloModel.Id.Value);
			Assert.Equal("Updated Integration Test", updatedKlo.Comment);

			// Act & Assert - Delete
			await _service.DeleteKloDataComment(kloModel.Id);
			var afterDelete = await _service.GetAllKloData();
			Assert.Empty(afterDelete);
		}

		[Fact]
		public async Task AddMultipleKloModels_WithRelations_ShouldPersistCorrectly()
		{
			// Arrange
			var klo1 = CreateTestKloModel("Hamburg Hauptbahnhof");
			var klo2 = CreateTestKloModel("Berlin Alexanderplatz");
			var klo3 = CreateTestKloModel("München Marienplatz");

			// Act
			await _service.AddKloCommentToData(klo1);
			await _service.AddKloCommentToData(klo2);
			await _service.AddKloCommentToData(klo3);

			// Assert
			var allKlos = await _service.GetAllKloData();
			Assert.Equal(3, allKlos.Count);

			// Verify all Elements are loaded
			Assert.All(allKlos, klo =>
			{
				Assert.NotNull(klo.Elements);
				Assert.Equal(2, klo.Elements.Count);
				Assert.NotNull(klo.Osm3s);
			});
		}

		[Fact]
		public async Task UpdateKloModel_WithChangedElements_ShouldPersistChanges()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Original");
			await _service.AddKloCommentToData(kloModel);

			// Act - Load and modify
			var loadedKlo = await _service.GetOneKloData(kloModel.Id.Value);
			loadedKlo.Comment = "Modified Comment";
			loadedKlo.PictureUrl = "https://example.com/new-picture.jpg";

			// Ändere das Dictionary und weise es neu zu, um den Setter zu triggern
			var tags = loadedKlo.Elements[0].Tags;
			tags["wheelchair"] = "limited";
			loadedKlo.Elements[0].Tags = tags; // Neu zuweisen!

			await _service.UpdateCommentData(loadedKlo);

			// Assert
			var updatedKlo = await _service.GetOneKloData(kloModel.Id.Value);
			Assert.Equal("Modified Comment", updatedKlo.Comment);
			Assert.Equal("https://example.com/new-picture.jpg", updatedKlo.PictureUrl);
			Assert.Equal("limited", updatedKlo.Elements[0].Tags["wheelchair"]);
		}

		[Fact]
		public async Task DeleteKloModel_ShouldAlsoCascadeDeleteRelatedEntities()
		{
			// Arrange
			var kloModel = CreateTestKloModel("To Delete");
			await _service.AddKloCommentToData(kloModel);
			var elementIds = kloModel.Elements.Select(e => e.Id).ToList();
			var osm3sId = kloModel.Osm3s.Id;

			// Act
			await _service.DeleteKloDataComment(kloModel.Id);

			// Assert - Check if related entities are also deleted (depends on cascade settings)
			var klo = await _service.GetOneKloData(kloModel.Id.Value);
			Assert.Null(klo);
		}

		[Fact]
		public async Task ConcurrentOperations_ShouldHandleMultipleAdds()
		{
			// Arrange
			var klos = Enumerable.Range(1, 10)
				.Select(i => CreateTestKloModel($"Klo {i}"))
				.ToList();

			// Act - Simulate concurrent adds
			var tasks = klos.Select(klo => _service.AddKloCommentToData(klo));
			await Task.WhenAll(tasks);

			// Assert
			var allKlos = await _service.GetAllKloData();
			Assert.Equal(10, allKlos.Count);
		}

		[Fact]
		public async Task GetAllKloData_WithLargeDataset_ShouldReturnAllRecords()
		{
			// Arrange - Add 50 records
			for (int i = 0; i < 50; i++)
			{
				var klo = CreateTestKloModel($"Bulk Test {i}");
				await _service.AddKloCommentToData(klo);
			}

			// Act
			var allKlos = await _service.GetAllKloData();

			// Assert
			Assert.Equal(50, allKlos.Count);
			Assert.All(allKlos, klo => Assert.NotNull(klo.Elements));
		}

		[Fact]
		public async Task DeleteNonExistentKlo_ShouldNotThrowException()
		{
			// Arrange
			var nonExistentId = Guid.NewGuid();

			// Act & Assert - Should not throw
			await _service.DeleteKloDataComment(nonExistentId);
			var allData = await _service.GetAllKloData();
			Assert.Empty(allData);
		}

		[Fact]
		public async Task GetOneKloData_AfterMultipleUpdates_ShouldReturnLatestVersion()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Version 1");
			await _service.AddKloCommentToData(kloModel);

			// Act - Multiple updates
			for (int i = 2; i <= 5; i++)
			{
				var klo = await _service.GetOneKloData(kloModel.Id.Value);
				klo.Comment = $"Version {i}";
				await _service.UpdateCommentData(klo);
			}

			// Assert
			var finalKlo = await _service.GetOneKloData(kloModel.Id.Value);
			Assert.Equal("Version 5", finalKlo.Comment);
		}

		[Fact]
		public async Task AddKloModel_WithComplexTags_ShouldPreserveAllData()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Complex Tags Test");
			kloModel.Elements[0].Tags = new Dictionary<string, string>
			{
				{ "amenity", "toilets" },
				{ "access", "public" },
				{ "fee", "no" },
				{ "wheelchair", "yes" },
				{ "name", "Öffentliche Toilette München" },
				{ "opening_hours", "24/7" },
				{ "operator", "Stadt München" },
				{ "description", "Saubere öffentliche Toilette im Zentrum" }
			};

			// Act
			await _service.AddKloCommentToData(kloModel);

			// Assert
			var savedKlo = await _service.GetOneKloData(kloModel.Id.Value);
			Assert.Equal(8, savedKlo.Elements[0].Tags.Count);
			Assert.Equal("Öffentliche Toilette München", savedKlo.Elements[0].Tags["name"]);
		}

		[Fact]
		public async Task UpdateCommentData_WithNullOsm3s_ShouldHandleGracefully()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Test");
			await _service.AddKloCommentToData(kloModel);

			var loadedKlo = await _service.GetOneKloData(kloModel.Id.Value);
			loadedKlo.Comment = "Updated";

			// Act & Assert
			await _service.UpdateCommentData(loadedKlo);
			var updatedKlo = await _service.GetOneKloData(kloModel.Id.Value);
			Assert.Equal("Updated", updatedKlo.Comment);
		}

		private KloModel CreateTestKloModel(string comment)
		{
			var kloId = Guid.NewGuid();
			return new KloModel
			{
				Id = kloId,
				Version = 0.6,
				Generator = "Integration Test Generator",
				Comment = comment,
				PictureUrl = "https://example.com/toilet.jpg",
				Osm3s = new Osm3s
				{
					Id = Guid.NewGuid(),
					TimestampOsmBase = DateTime.UtcNow,
					TimestampAreasBase = DateTime.UtcNow,
					Copyright = "© OpenStreetMap contributors"
				},
				Elements = new List<Element>
				{
					new Element
					{
						Id = Guid.NewGuid(),
						KloModelId = kloId,
						Type = "node",
						ElementId = 123456789,
						Lat = 53.5801097,
						Lon = 9.8859876,
						Tags = new Dictionary<string, string>
						{
							{ "amenity", "toilets" },
							{ "access", "public" },
							{ "fee", "no" },
							{ "wheelchair", "yes" }
						}
					},
					new Element
					{
						Id = Guid.NewGuid(),
						KloModelId = kloId,
						Type = "node",
						ElementId = 987654321,
						Lat = 53.5511,
						Lon = 9.9937,
						Tags = new Dictionary<string, string>
						{
							{ "amenity", "toilets" },
							{ "access", "customers" },
							{ "fee", "yes" }
						}
					}
				}
			};
		}

		public void Dispose()
		{
			_context.Database.EnsureDeleted();
			_context.Dispose();
		}

	}
}