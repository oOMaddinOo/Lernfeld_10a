using WaterDrop.Components.Services;

namespace WaterDropTests.UnitTests
{
	[Trait("Category", "Unit")]
	public class KloServiceTests
	{
		[Fact]
		public async Task GetToilets_WithValidResponse_ShouldReturnKloModel()
		{
			// Arrange
			var service = new kloService();
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity("Hamburg")
				.SetTimeout(30);

			// Act
			var result = await service.GetToilets(queryBuilder);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Elements);
		}

		[Fact]
		public async Task GetToilets_WithDifferentCity_ShouldExecuteQuery()
		{
			// Arrange
			var service = new kloService();
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity("Berlin")
				.SetTimeout(25);

			// Act
			var result = await service.GetToilets(queryBuilder);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Elements);
		}

		[Fact]
		public async Task GetToilets_WithAccessTypeFilter_ShouldExecuteQuery()
		{
			// Arrange
			var service = new kloService();
			var queryBuilder = new ToiletQueryBuilder()
				.SetCity("München")
				.SetAccessType("public")
				.IncludeFeeInformation(true);

			// Act
			var result = await service.GetToilets(queryBuilder);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Elements);
		}

		[Fact]
		public void ToiletQueryBuilder_Build_ShouldContainCity()
		{
			// Arrange
			var builder = new ToiletQueryBuilder().SetCity("Köln");

			// Act
			var query = builder.Build();

			// Assert
			Assert.Contains("Köln", query);
			Assert.Contains("[out:json]", query);
		}

		[Fact]
		public void ToiletQueryBuilder_Build_WithAccessType_ShouldIncludeAccessFilter()
		{
			// Arrange
			var builder = new ToiletQueryBuilder()
				.SetAccessType("public");

			// Act
			var query = builder.Build();

			// Assert
			Assert.Contains(@"[""access""=""public""]", query);
		}

		[Fact]
		public void ToiletQueryBuilder_Build_WithFee_ShouldIncludeFeeFilter()
		{
			// Arrange
			var builder = new ToiletQueryBuilder()
				.IncludeFeeInformation(true);

			// Act
			var query = builder.Build();

			// Assert
			Assert.Contains(@"[""fee""]", query);
		}

		[Fact]
		public void ToiletQueryBuilder_Build_WithCustomTimeout_ShouldSetTimeout()
		{
			// Arrange
			var builder = new ToiletQueryBuilder()
				.SetTimeout(45);

			// Act
			var query = builder.Build();

			// Assert
			Assert.Contains("[timeout:45]", query);
		}

		[Fact]
		public void ToiletQueryBuilder_Chaining_ShouldAllowFluentAPI()
		{
			// Arrange & Act
			var query = new ToiletQueryBuilder()
				.SetCity("Frankfurt")
				.SetTimeout(20)
				.SetAccessType("customers")
				.IncludeFeeInformation(true)
				.Build();

			// Assert
			Assert.Contains("Frankfurt", query);
			Assert.Contains("[timeout:20]", query);
			Assert.Contains(@"[""access""=""customers""]", query);
			Assert.Contains(@"[""fee""]", query);
		}
	}
}
