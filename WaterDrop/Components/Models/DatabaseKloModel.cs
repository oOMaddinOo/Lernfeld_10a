namespace WaterDrop.Components.Models
{
	public class DatabaseKloModel
	{
		public Guid Id { get; set; }
		public long ElementId { get; set; }
		public string? Comment { get; set; }

		public string? PictureUrl { get; set; }
	}
}
