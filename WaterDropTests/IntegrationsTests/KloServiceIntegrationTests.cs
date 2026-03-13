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
			var kloModel = CreateTestKloModel("Integration Test", 123456);

			// Act & Assert - Add
			await _service.AddKloCommentToData(kloModel);
			var allData = await _service.GetAllKloData();
			Assert.Single(allData);
			Assert.Equal("Integration Test", allData[0].Comment);
			Assert.Equal(123456, allData[0].ElementId);

			// Act & Assert - Get One
			var singleKlo = await _service.GetOneKloData(kloModel.Id);
			Assert.NotNull(singleKlo);
			Assert.Equal("Integration Test", singleKlo.Comment);

			// Act & Assert - Update
			singleKlo.Comment = "Updated Integration Test";
			await _service.UpdateCommentData(singleKlo);
			var updatedKlo = await _service.GetOneKloData(kloModel.Id);
			Assert.Equal("Updated Integration Test", updatedKlo.Comment);

			// Act & Assert - Delete
			await _service.DeleteKloDataComment(kloModel.Id);
			var afterDelete = await _service.GetAllKloData();
			Assert.Empty(afterDelete);
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
			var allKlos = await _service.GetAllKloData();
			Assert.Equal(3, allKlos.Count);

			// Verify all have correct data
			Assert.All(allKlos, klo =>
			{
				Assert.NotNull(klo.Comment);
				Assert.NotEqual(0, klo.ElementId);
			});
		}

		[Fact]
		public async Task UpdateKloModel_WithChangedProperties_ShouldPersistChanges()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Original", 444444);
			await _service.AddKloCommentToData(kloModel);

			// Act - Load and modify
			var loadedKlo = await _service.GetOneKloData(kloModel.Id);
			loadedKlo.Comment = "Modified Comment";
			loadedKlo.PictureUrl = "https://example.com/new-picture.jpg";

			await _service.UpdateCommentData(loadedKlo);

			// Assert
			var updatedKlo = await _service.GetOneKloData(kloModel.Id);
			Assert.Equal("Modified Comment", updatedKlo.Comment);
			Assert.Equal("https://example.com/new-picture.jpg", updatedKlo.PictureUrl);
			Assert.Equal(444444, updatedKlo.ElementId);
		}

		[Fact]
		public async Task DeleteKloModel_ShouldRemoveFromDatabase()
		{
			// Arrange
			var kloModel = CreateTestKloModel("To Delete", 555555);
			await _service.AddKloCommentToData(kloModel);

			// Verify it exists
			var existingKlo = await _service.GetOneKloData(kloModel.Id);
			Assert.NotNull(existingKlo);

			// Act
			await _service.DeleteKloDataComment(kloModel.Id);

			// Assert
			var klo = await _service.GetOneKloData(kloModel.Id);
			Assert.Null(klo);
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
			var allKlos = await _service.GetAllKloData();
			Assert.Equal(10, allKlos.Count);
			Assert.Equal(10, allKlos.Select(k => k.ElementId).Distinct().Count());
		}

		[Fact]
		public async Task GetAllKloData_WithLargeDataset_ShouldReturnAllRecords()
		{
			// Arrange - Add 50 records
			for (int i = 0; i < 50; i++)
			{
				var klo = CreateTestKloModel($"Bulk Test {i}", 2000000 + i);
				await _service.AddKloCommentToData(klo);
			}

			// Act
			var allKlos = await _service.GetAllKloData();

			// Assert
			Assert.Equal(50, allKlos.Count);
			Assert.All(allKlos, klo =>
			{
				Assert.NotNull(klo.Comment);
				Assert.NotEqual(0, klo.ElementId);
			});
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
			var kloModel = CreateTestKloModel("Version 1", 666666);
			await _service.AddKloCommentToData(kloModel);

			// Act - Multiple updates
			for (int i = 2; i <= 5; i++)
			{
				var klo = await _service.GetOneKloData(kloModel.Id);
				klo.Comment = $"Version {i}";
				await _service.UpdateCommentData(klo);
			}

			// Assert
			var finalKlo = await _service.GetOneKloData(kloModel.Id);
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
		public async Task UpdateKloModel_ShouldNotChangeElementId()
		{
			// Arrange
			var originalElementId = 123123123L;
			var kloModel = CreateTestKloModel("Original", originalElementId);
			await _service.AddKloCommentToData(kloModel);

			// Act
			var loadedKlo = await _service.GetOneKloData(kloModel.Id);
			loadedKlo.Comment = "Updated";
			loadedKlo.PictureUrl = "https://example.com/updated.jpg";
			await _service.UpdateCommentData(loadedKlo);

			// Assert
			var updatedKlo = await _service.GetOneKloData(kloModel.Id);
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
			var allKlos = await _service.GetAllKloData();
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
			var savedKlo = await _service.GetOneKloData(kloModel.Id);
			Assert.NotNull(savedKlo);
			Assert.Null(savedKlo.PictureUrl);
			Assert.Equal("No Picture", savedKlo.Comment);
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
			_context.Database.EnsureDeleted();
			_context.Dispose();
		}
	}
}