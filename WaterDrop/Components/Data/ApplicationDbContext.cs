using Microsoft.EntityFrameworkCore;
using WaterDrop.Components.Models;

namespace WaterDrop.Components.Data
{
	public class ApplicationDbContext : DbContext
	{
		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
		{
		}

		public DbSet<DatabaseKloModel> DatabaseKloModel { get; set; }
		
		public DbSet<ToiletData> Toilets { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<ToiletData>(entity =>
			{
				entity.HasKey(e => e.Id);

				entity.HasIndex(e => e.City);

				entity.HasIndex(e => new { e.Lat, e.Lon });

				entity.HasIndex(e => e.ElementId).IsUnique();
			});
		}
	}
}
