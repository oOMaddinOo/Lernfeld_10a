using WaterDrop.Components.Services;
using WaterDrop.Components.Models;
using Xunit;

namespace WaterDropTests.IntegrationsTests
{
	[Trait("Category", "Integration")]
	public class KloServiceIntegrationTests
	{
		private readonly kloService _service;

		public KloServiceIntegrationTests()
		{
			_service = new kloService();
		}

		[Fact]
		public async Task GetToilets_WithValidCity_ReturnsRealData()
		{
			// Arrange
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity("Hamburg")
				.SetTimeout(30);

			// Act
			var result = await _service.GetToilets(queryBuilder);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Elements);
			Assert.True(result.Elements.Count > 0, "Hamburg sollte Toiletten haben");

			// Validiere erste Toilette hat gültige Koordinaten
			var firstToilet = result.Elements[0];
			Assert.NotNull(firstToilet.Lat);
			Assert.NotNull(firstToilet.Lon);
			Assert.InRange(firstToilet.Lat.Value, 53.0, 54.0); // Hamburg Latitude
			Assert.InRange(firstToilet.Lon.Value, 9.0, 11.0);  // Hamburg Longitude
		}

		[Fact]
		public async Task GetToilets_WithMultipleCities_ReturnsCorrectRegions()
		{
			// Arrange
			var cities = new[] { "Berlin", "München", "Köln" };

			foreach (var city in cities)
			{
				var queryBuilder = new ToiletQueryBuilder()
					.SetCity(city)
					.SetTimeout(30);

				// Act
				var result = await _service.GetToilets(queryBuilder);

				// Assert
				Assert.NotNull(result);
				Assert.True(result.Elements.Count > 0, $"{city} sollte Toiletten haben");

				await Task.Delay(1000); // Rate limiting - API nicht überlasten
			}
		}

		[Fact]
		public async Task GetToilets_WithAccessFilter_ReturnsFilteredResults()
		{
			// Arrange
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity("Berlin")
				.SetAccessType("public")
				.SetTimeout(30);

			// Act
			var result = await _service.GetToilets(queryBuilder);

			// Assert
			Assert.NotNull(result);
			Assert.True(result.Elements.Count > 0);

			// Prüfe dass Elemente public access haben (wenn Tags vorhanden)
			var elementsWithAccessTag = result.Elements
				.Where(e => e.Tags?.ContainsKey("access") == true)
				.ToList();

			if (elementsWithAccessTag.Any())
			{
				Assert.All(elementsWithAccessTag,
					e => Assert.Equal("public", e.Tags!["access"]));
			}
		}

		[Fact]
		public async Task GetToilets_WithFeeInformation_ContainsFeeTag()
		{
			// Arrange
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity("Hamburg")
				.IncludeFeeInformation(true)
				.SetTimeout(30);

			// Act
			var result = await _service.GetToilets(queryBuilder);

			// Assert
			Assert.NotNull(result);

			// Mindestens einige Elemente sollten fee-Information haben
			var elementsWithFee = result.Elements
				.Where(e => e.Tags?.ContainsKey("fee") == true)
				.ToList();

			Assert.NotEmpty(elementsWithFee);
		}

		[Fact]
		public async Task GetToilets_WithInvalidCity_ReturnsEmptyOrNull()
		{
			// Arrange
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity("NichtExistierendeStadt123456")
				.SetTimeout(15);

			// Act
			var result = await _service.GetToilets(queryBuilder);

			// Assert - Je nach API-Response
			if (result != null)
			{
				Assert.Empty(result.Elements);
			}
		}

		[Fact]
		public async Task GetToilets_ApiResponse_HasCorrectStructure()
		{
			// Arrange
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity("München")
				.SetTimeout(30);

			// Act
			var result = await _service.GetToilets(queryBuilder);

			// Assert - Validiere Datenstruktur
			Assert.NotNull(result);
			Assert.NotNull(result.Elements);

			foreach (var element in result.Elements.Take(5))
			{
				Assert.NotNull(element.Type);
				Assert.True(element.Type == "node" || element.Type == "way",
					"Type sollte 'node' oder 'way' sein");

				if (element.Lat.HasValue && element.Lon.HasValue)
				{
					Assert.InRange(element.Lat.Value, -90, 90);
					Assert.InRange(element.Lon.Value, -180, 180);
				}
			}
		}

		[Theory]
		[InlineData("Hamburg", 53.55, 10.0)]
		[InlineData("Berlin", 52.52, 13.4)]
		[InlineData("München", 48.13, 11.5)]
		public async Task GetToilets_VerifiesGeographicLocation(
			string city, double expectedLat, double expectedLon)
		{
			// Arrange
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity(city)
				.SetTimeout(30);

			// Act
			var result = await _service.GetToilets(queryBuilder);

			// Assert
			Assert.NotNull(result);
			Assert.True(result.Elements.Count > 0);

			var firstElement = result.Elements.First(e => e.Lat.HasValue && e.Lon.HasValue);

			// Toleranz von ±0.5 Grad
			Assert.InRange(firstElement.Lat!.Value, expectedLat - 0.5, expectedLat + 0.5);
			Assert.InRange(firstElement.Lon!.Value, expectedLon - 0.5, expectedLon + 0.5);
		}

		[Fact]
		public async Task GetToilets_ReturnsElementsWithTags()
		{
			// Arrange
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity("Hamburg")
				.SetTimeout(30);

			// Act
			var result = await _service.GetToilets(queryBuilder);

			// Assert
			Assert.NotNull(result);
			var elementsWithTags = result.Elements.Where(e => e.Tags != null && e.Tags.Count > 0);
			Assert.NotEmpty(elementsWithTags);

			// Prüfe typische Toiletten-Tags
			var firstWithTags = elementsWithTags.First();
			Assert.True(
				firstWithTags.Tags!.ContainsKey("amenity") ||
				firstWithTags.Tags.ContainsKey("toilets") ||
				firstWithTags.Tags.ContainsKey("name"),
				"Element sollte relevante Tags haben"
			);
		}

		[Fact(Skip = "Nur manuell ausführen - sehr langsamer Test")]
		public async Task GetToilets_StressTest_MultipleSequentialCalls()
		{
			// Test für Stabilität bei mehreren Aufrufen
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity("Hamburg")
				.SetTimeout(20);

			for (int i = 0; i < 5; i++)
			{
				var result = await _service.GetToilets(queryBuilder);
				Assert.NotNull(result);
				Assert.True(result.Elements.Count > 0);

				await Task.Delay(2000); // Rate limiting - 2 Sekunden zwischen Requests
			}
		}
	}
}