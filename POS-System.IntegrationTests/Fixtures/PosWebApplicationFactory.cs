using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Data.Database;
using POS_System.Data.Identity;

namespace POS_System.IntegrationTests.Fixtures;

public class PosWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Username=postgres;Password=sekmesatspet;Database=postgres_test;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LocalConnection"] = TestConnectionString,
                ["BusinessDetails:FileName"] = "business-details.json",
                ["BusinessDetails:RelativePath"] = ""
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>))
                .ToList();
            foreach (var d in descriptors)
                services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(
                    TestConnectionString,
                    npgsql => npgsql.MigrationsAssembly("POS-System.Data")));

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();

            SeedRolesAsync(scope.ServiceProvider).GetAwaiter().GetResult();
        });
    }

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        string[] roles = ["Super admin", "Service provider", "Cashier", "Owner", "None"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
        }
    }
}
