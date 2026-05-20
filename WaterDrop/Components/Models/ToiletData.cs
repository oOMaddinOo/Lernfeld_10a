using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WaterDrop.Components.Models
{
    [Index(nameof(ElementId), IsUnique = true)]
    [Index(nameof(City))]
    [Index(nameof(Lat), nameof(Lon))]
    public class ToiletData
    {
        [Key]
        public Guid Id { get; set; }

        public long ElementId { get; set; }

        public double Lat { get; set; }
        public double Lon { get; set; }

        public string Type { get; set; } = "node";

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(200)]
        public string? Name { get; set; }

        public bool? IsAccessible { get; set; }
        public bool? HasChangingTable { get; set; }
        public bool? HasFee { get; set; } 
        public string? AccessType { get; set; }
        public string? OpeningHours { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? TagsJson { get; set; }

        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdated { get; set; }

        public string? UserComment { get; set; }
        public string? UserPictureUrl { get; set; }

        [NotMapped]
        public Dictionary<string, string>? Tags
        {
            get => string.IsNullOrEmpty(TagsJson) 
                ? null 
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(TagsJson);
            set => TagsJson = value == null 
                ? null 
                : System.Text.Json.JsonSerializer.Serialize(value);
        }
    }
}