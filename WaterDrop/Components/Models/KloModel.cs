using Newtonsoft.Json;

namespace WaterDrop.Components.Models
{
	public class KloModel
	{
		[JsonProperty("version")]
		public double Version { get; set; }

		[JsonProperty("generator")]
		public string Generator { get; set; }

		[JsonProperty("osm3s")]
		public Osm3s Osm3s { get; set; }

		[JsonProperty("elements")]
		public List<Element> Elements { get; set; }
	}

	public class Osm3s
	{
		[JsonProperty("timestamp_osm_base")]
		public DateTime TimestampOsmBase { get; set; }

		[JsonProperty("timestamp_areas_base")]
		public DateTime TimestampAreasBase { get; set; }

		[JsonProperty("copyright")]
		public string Copyright { get; set; }
	}

	public class Element
	{
		[JsonProperty("type")]
		public string Type { get; set; }

		[JsonProperty("id")]
		public long Id { get; set; }

		[JsonProperty("lat")]
		public double? Lat { get; set; }

		[JsonProperty("lon")]
		public double? Lon { get; set; }

		// Dynamische Tags
		[JsonProperty("tags")]
		public Dictionary<string, string> Tags { get; set; }
	}
}