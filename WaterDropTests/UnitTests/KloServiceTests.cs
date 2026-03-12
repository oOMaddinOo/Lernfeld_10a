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
			var kloModel = CreateTestKloModel("Test Kommentar");

			// Act
			await _service.AddKloCommentToData(kloModel);

			// Assert
			var result = await _context.KloModel.FindAsync(kloModel.Id);
			Assert.NotNull(result);
			Assert.Equal("Test Kommentar", result.Comment);
			Assert.Single(_context.KloModel);
		}

		[Fact]
		public async Task GetAllKloData_ShouldReturnAllKloModels()
		{
			// Arrange
			var klo1 = CreateTestKloModel("Kommentar 1");
			var klo2 = CreateTestKloModel("Kommentar 2");
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
		public async Task GetAllKloData_ShouldIncludeElementsAndOsm3s()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Test mit Relations");
			await _service.AddKloCommentToData(kloModel);

			// Act
			var result = await _service.GetAllKloData();

			// Assert
			Assert.Single(result);
			Assert.NotNull(result[0].Elements);
			Assert.Equal(2, result[0].Elements.Count);
			Assert.NotNull(result[0].Osm3s);
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
			var kloModel = CreateTestKloModel("Zu löschen");
			await _service.AddKloCommentToData(kloModel);

			// Act
			await _service.DeleteKloDataComment(kloModel.Id);

			// Assert
			var result = await _context.KloModel.FindAsync(kloModel.Id);
			Assert.Null(result);
			Assert.Empty(_context.KloModel);
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
			var kloModel = CreateTestKloModel("Original Kommentar");
			await _service.AddKloCommentToData(kloModel);

			// Detach to simulate a fresh context
			_context.Entry(kloModel).State = EntityState.Detached;

			var updatedKlo = await _context.KloModel
				.Include(k => k.Elements)
				.Include(k => k.Osm3s)
				.FirstAsync(k => k.Id == kloModel.Id);
			updatedKlo.Comment = "Aktualisierter Kommentar";

			// Act
			await _service.UpdateCommentData(updatedKlo);
			await _context.SaveChangesAsync();

			// Assert
			var result = await _context.KloModel.FindAsync(kloModel.Id);
			Assert.NotNull(result);
			Assert.Equal("Aktualisierter Kommentar", result.Comment);
		}

		[Fact]
		public async Task GetOneKloData_ShouldReturnSpecificKloModel()
		{
			// Arrange
			var klo1 = CreateTestKloModel("Kommentar 1");
			var klo2 = CreateTestKloModel("Kommentar 2");
			await _service.AddKloCommentToData(klo1);
			await _service.AddKloCommentToData(klo2);

			// Act
			var result = await _service.GetOneKloData(klo1.Id.Value);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(klo1.Id, result.Id);
			Assert.Equal("Kommentar 1", result.Comment);
		}

		[Fact]
		public async Task GetOneKloData_ShouldIncludeElementsAndOsm3s()
		{
			// Arrange
			var kloModel = CreateTestKloModel("Test");
			await _service.AddKloCommentToData(kloModel);

			// Act
			var result = await _service.GetOneKloData(kloModel.Id.Value);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Elements);
			Assert.Equal(2, result.Elements.Count);
			Assert.NotNull(result.Osm3s);
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

		private KloModel CreateTestKloModel(string comment)
		{
			var kloId = Guid.NewGuid();
			return new KloModel
			{
				Id = kloId,
				Version = 0.6,
				Generator = "Test Generator",
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
							{ "access", "public" }
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
