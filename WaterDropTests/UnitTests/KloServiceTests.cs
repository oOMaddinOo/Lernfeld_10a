using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
		private readonly IMemoryCache _cache;

		public KloServiceTests()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.Options;
			_cache = new MemoryCache(new MemoryCacheOptions());
			_context = new ApplicationDbContext(options);
			_service = new kloService(_context, _cache);
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
		}
	}
}
