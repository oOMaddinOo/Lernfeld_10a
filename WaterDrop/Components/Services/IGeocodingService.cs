namespace WaterDrop.Components.Services
{
    public interface IGeocodingService
    {
        Task<BoundingBox?> GetCityBoundingBoxAsync(string city);
        IReadOnlyDictionary<string, BoundingBox> GetAllCities();
    }
}