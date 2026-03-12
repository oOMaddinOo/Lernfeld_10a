using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WaterDrop.Components.Data;
using WaterDrop.Components.Models;

namespace WaterDrop.Components.Services
{
	public class kloService
	{
		private static readonly HttpClient _httpClient = new HttpClient();
		private static ApplicationDbContext _context;

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


		public async Task AddKloCommentToData(KloModel klomodel)
		{
			_context.KloModel.Add(klomodel);
			await _context.SaveChangesAsync();
		}

		public async Task<List<KloModel>> GetAllKloData()
		{
			return await _context.KloModel.Include(k => k.Elements).Include(k => k.Osm3s).ToListAsync();
		}

		public async Task DeleteKloDataComment(Guid? kloId)
		{
			if (kloId == null)
			{
				throw new ArgumentNullException(nameof(kloId));
			}

			var klo = await _context.KloModel.FindAsync(kloId);

			if (klo == null)
			{
				return;
			}

			_context.KloModel.Remove(klo);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateCommentData(KloModel kloModel)
		{
			_context.KloModel.Update(kloModel);
			await _context.SaveChangesAsync();
		}

		public async Task<KloModel?> GetOneKloData(Guid kloId)
		{
			return await _context.KloModel.Include(k => k.Elements).Include(k => k.Osm3s).FirstOrDefaultAsync(k => k.Id == kloId);
		}
	}
}