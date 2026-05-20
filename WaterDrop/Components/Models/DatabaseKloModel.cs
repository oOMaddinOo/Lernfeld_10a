namespace WaterDrop.Components.Models
{
	public class DatabaseKloModel
	{
		public Guid Id { get; set; }
		public long ElementId { get; set; }
		public string? Comment { get; set; }

		public string? PictureUrl { get; set; }

		// Erstellungszeitpunkt des Reviews. Nullable, damit bestehende DB-Zeilen
		// (die diese Spalte vor der AddCreatedAt-Migration noch nicht hatten)
		// weiterhin gültig bleiben. Neue Einträge bekommen DateTime.UtcNow
		// gesetzt — siehe kloService.AddKloCommentToData.
		public DateTime? CreatedAt { get; set; }
	}
}
