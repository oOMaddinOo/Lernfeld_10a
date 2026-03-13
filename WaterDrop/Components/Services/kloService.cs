using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WaterDrop.Components.Data;
using WaterDrop.Components.Models;

namespace WaterDrop.Components.Services
{
	public class kloService
	{
		private static readonly HttpClient _httpClient = new HttpClient();
		private ApplicationDbContext _context;

		public kloService(ApplicationDbContext context)
		{
			_context = context;
		}

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


		public async Task AddKloCommentToData(DatabaseKloModel klomodel)
		{
			_context.DatabaseKloModel.Add(klomodel);
			await _context.SaveChangesAsync();
		}

		public async Task<List<DatabaseKloModel>> GetAllKloData()
		{
			return await _context.DatabaseKloModel.ToListAsync();
		}

		public async Task<DatabaseKloModel> GetKloByElementId(long elementId)
		{
			return await _context.DatabaseKloModel
		.Where(e => e.ElementId == elementId)
		.FirstOrDefaultAsync();
		}

		public async Task DeleteKloDataComment(Guid? kloId)
		{
			if (kloId == null)
			{
				throw new ArgumentNullException(nameof(kloId));
			}

			var klo = await _context.DatabaseKloModel.FindAsync(kloId);

			if (klo == null)
			{
				return;
			}

			_context.DatabaseKloModel.Remove(klo);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateCommentData(DatabaseKloModel kloModel)
		{
			_context.DatabaseKloModel.Update(kloModel);
			await _context.SaveChangesAsync();
		}

		public async Task<DatabaseKloModel?> GetOneKloData(Guid kloId)
		{
			return await _context.DatabaseKloModel.FirstOrDefaultAsync(k => k.Id == kloId);
		}
	}
}