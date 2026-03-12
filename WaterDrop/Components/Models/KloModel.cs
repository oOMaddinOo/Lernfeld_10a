using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WaterDrop.Components.Models
{


	public class KloModel
	{
		[Key]
		public Guid? Id { get; set; }

		[JsonProperty("version")]
		public double Version { get; set; }

		[JsonProperty("generator")]
		public string Generator { get; set; }

		[JsonProperty("osm3s")]
		public Osm3s Osm3s { get; set; }

		[ForeignKey("Osm3s")]
		public Guid? Osm3sId { get; set; }

		[JsonProperty("elements")]
		public List<Element> Elements { get; set; }

		public string? Comment { get; set; }

		public string? PictureUrl { get; set; }
	}

	public class Osm3s
	{
		[Key]
		public Guid Id { get; set; }

		[JsonProperty("timestamp_osm_base")]
		public DateTime TimestampOsmBase { get; set; }

		[JsonProperty("timestamp_areas_base")]
		public DateTime TimestampAreasBase { get; set; }

		[JsonProperty("copyright")]
		public string Copyright { get; set; }
	}

	public class Element
	{
		[Key]
		public Guid Id { get; set; }

		[JsonProperty("type")]
		public string Type { get; set; }

		[JsonProperty("id")]
		public long ElementId { get; set; }

		[JsonProperty("lat")]
		public double? Lat { get; set; }

		[JsonProperty("lon")]
		public double? Lon { get; set; }

		[JsonProperty("tags")]
		[Column(TypeName = "nvarchar(max)")]
		public string TagsJson { get; set; }

		[NotMapped]
		public Dictionary<string, string> Tags
		{
			get => string.IsNullOrEmpty(TagsJson) ? new() : JsonConvert.DeserializeObject<Dictionary<string, string>>(TagsJson);
			set => TagsJson = JsonConvert.SerializeObject(value);
		}

		[ForeignKey("KloModel")]
		public Guid? KloModelId { get; set; }
	}
}