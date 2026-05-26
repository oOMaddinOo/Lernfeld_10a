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

            // SignalR Konfiguration für Azure
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            });

            builder.Services.AddDbContext<ApplicationDbContext>(options => 
                options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnection")));

            builder.Services.AddScoped<kloService>();
			builder.Services.AddScoped<IGeocodingService, GeocodingService>();

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

            app.UseWebSockets();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

			app.Run();
        }
    }
}
