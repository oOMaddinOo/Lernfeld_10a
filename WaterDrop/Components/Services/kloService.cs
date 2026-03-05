using Newtonsoft.Json;
using WaterDrop.Components.Models;

namespace WaterDrop.Components.Services
{
	public class kloService
	{
		private static readonly HttpClient _httpClient = new HttpClient();

		public async Task<KloModel> GetToilets(ToiletQueryBuilder queryBuilder)
		{
			var query = queryBuilder.Build();
			return await ExecuteQuery(query);
		}

		private async Task<KloModel> ExecuteQuery(string query)
		{
			var url = "https://overpass-api.de/api/interpreter";
			var content = new FormUrlEncodedContent(new[]
			{
				new KeyValuePair<string, string>("data", query)
			});

			try
			{
				var response = await _httpClient.PostAsync(url, content);
				response.EnsureSuccessStatusCode();
				var json = await response.Content.ReadAsStringAsync();

				var result = JsonConvert.DeserializeObject<KloModel>(json);

				if (result != null && result.Elements == null)
				{
					result.Elements = new List<Element>();
				}

				return result;
			}
			catch (HttpRequestException httpEx)
			{
				Console.WriteLine($"HTTP Error fetching toilets: {httpEx.Message}");
				return new KloModel { Elements = new List<Element>() };
			}
			catch (JsonException jsonEx)
			{
				Console.WriteLine($"JSON Deserialization Error: {jsonEx.Message}");
				return new KloModel { Elements = new List<Element>() };
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching toilets: {ex.Message}");
				return new KloModel { Elements = new List<Element>() };
			}
		}
	}
}