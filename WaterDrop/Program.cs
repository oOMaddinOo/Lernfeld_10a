using Microsoft.EntityFrameworkCore;
using WaterDrop.Components;
using WaterDrop.Components.Data;
using WaterDrop.Components.Services;

namespace WaterDrop
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddDbContext<ApplicationDbContext>(options => 
                options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnection")));

            builder.Services.AddScoped<kloService>();
			builder.Services.AddScoped<IGeocodingService, GeocodingService>();

			builder.Services.AddMemoryCache();
			builder.Services.AddHostedService<DrinkingWaterSeeder>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

			app.Run();
        }
    }
}
