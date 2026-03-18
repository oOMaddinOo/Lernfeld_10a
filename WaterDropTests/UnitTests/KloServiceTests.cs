using Microsoft.EntityFrameworkCore;
using WaterDrop.Components.Data;
using WaterDrop.Components.Models;
using WaterDrop.Components.Services;

namespace WaterDropTests.UnitTests
{
	[Trait("Category", "Unit")]
	public class KloServiceTests
	{
		private readonly ApplicationDbContext _context;
		private readonly kloService _service;

		public KloServiceTests()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.Options;

			_context = new ApplicationDbContext(options);
			_service = new kloService(_context);
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
		public async Task GetAllKloData_ShouldReturnAllKloModels()
		{
			// Arrange
			var klo1 = CreateTestKloModel("Kommentar 1", 111111);
			var klo2 = CreateTestKloModel("Kommentar 2", 222222);
			await _service.AddKloCommentToData(klo1);
			await _service.AddKloCommentToData(klo2);

			// Act
			var result = await _service.GetAllKloData();

			// Assert
			Assert.NotNull(result);
			Assert.Equal(2, result.Count);
			Assert.Contains(result, k => k.Comment == "Kommentar 1");
			Assert.Contains(result, k => k.Comment == "Kommentar 2");
		}

		[Fact]
		public async Task GetAllKloData_WhenEmpty_ShouldReturnEmptyList()
		{
			// Act
			var result = await _service.GetAllKloData();

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
		}

		[Fact]
		public async Task DeleteKloDataComment_ShouldRemoveKloModel()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Zu löschen", 333333);
			await _service.AddKloCommentToData(kloModel);

			// Act
			await _service.DeleteKloDataComment(kloModel.Id);

			// Assert
			var result = await _context.DatabaseKloModel.FindAsync(kloModel.Id);
			Assert.Null(result);
			Assert.Empty(_context.DatabaseKloModel);
		}

		[Fact]
		public async Task DeleteKloDataComment_WithNonExistentId_ShouldNotThrowException()
		{
			// Arrange
			var nonExistentId = Guid.NewGuid();

			// Act & Assert - sollte nicht werfen
			await _service.DeleteKloDataComment(nonExistentId);

			// Verify nothing was deleted
			var allData = await _service.GetAllKloData();
			Assert.Empty(allData);
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
		public async Task GetOneKloData_ShouldReturnSpecificKloModel()
		{
			// Arrange
			var klo1 = CreateTestKloModel("Kommentar 1", 555555);
			var klo2 = CreateTestKloModel("Kommentar 2", 666666);
			await _service.AddKloCommentToData(klo1);
			await _service.AddKloCommentToData(klo2);

			// Act
			var result = await _service.GetOneKloData(klo1.Id);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(klo1.Id, result.Id);
			Assert.Equal("Kommentar 1", result.Comment);
			Assert.Equal(555555, result.ElementId);
		}

		[Fact]
		public async Task GetOneKloData_WithNonExistentId_ShouldReturnNull()
		{
			// Arrange
			var nonExistentId = Guid.NewGuid();

			// Act
			var result = await _service.GetOneKloData(nonExistentId);

			// Assert
			Assert.Null(result);
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
		}
	}
}
