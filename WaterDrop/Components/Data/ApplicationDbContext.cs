using Microsoft.EntityFrameworkCore;
using WaterDrop.Components.Models;

namespace WaterDrop.Components.Data
{
	public class ApplicationDbContext : DbContext
	{
		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
		{
		}

		public DbSet<KloModel> KloModel { get; set; }
		public DbSet<Osm3s> Osm3s { get; set; }
		public DbSet<Element> Elements { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// KloModel Konfiguration
			modelBuilder.Entity<KloModel>(entity =>
			{
				entity.HasKey(e => e.Id);
				
				entity.HasOne(e => e.Osm3s)
					.WithMany()
					.HasForeignKey(e => e.Osm3sId)
					.OnDelete(DeleteBehavior.SetNull);

				entity.HasMany(e => e.Elements)
					.WithOne()
					.HasForeignKey(e => e.KloModelId)
					.OnDelete(DeleteBehavior.Cascade);
			});

			// Element Konfiguration
			modelBuilder.Entity<Element>(entity =>
			{
				entity.HasKey(e => e.Id);
				
				// TagsJson wird als JSON gespeichert, Tags wird ignoriert
				entity.Ignore(e => e.Tags);
				
				entity.Property(e => e.TagsJson)
					.HasColumnName("Tags")
					.HasColumnType("nvarchar(max)");
			});

			// Osm3s Konfiguration
			modelBuilder.Entity<Osm3s>(entity =>
			{
				entity.HasKey(e => e.Id);
			});
		}
	}
}
