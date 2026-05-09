using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using POS_System.Data.Database;
using System.Text.Json;

namespace POS_System.IntegrationTests.TestSupport;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName;
    private readonly string _businessDetailsDirectory;
    private readonly string _businessDetailsFileName = "business-details.json";

    public string BusinessDetailsPath => Path.Combine(_businessDetailsDirectory, _businessDetailsFileName);

    public CustomWebApplicationFactory(string databaseName)
    {
        _databaseName = databaseName;
        _businessDetailsDirectory = Path.Combine(Path.GetTempPath(), "pos-system-integration-tests", databaseName);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        Directory.CreateDirectory(_businessDetailsDirectory);
        var businessDetailsPath = Path.Combine(_businessDetailsDirectory, _businessDetailsFileName);
        if (!File.Exists(businessDetailsPath))
        {
            File.WriteAllText(
                businessDetailsPath,
                JsonSerializer.Serialize(new
                {
                    BusinessName = "Test Business",
                    BusinessEmail = "test@example.com",
                    BusinessPhone = "123456789",
                    Country = "Test Country",
                    City = "Test City",
                    Street = "Test Street",
                    HouseNumber = 1,
                    FlatNumber = (int?)null
                }));
        }

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["BusinessDetails:FileName"] = _businessDetailsFileName,
                ["BusinessDetails:RelativePath"] = _businessDetailsDirectory + Path.DirectorySeparatorChar
            };

            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureWebHost(webHostBuilder =>
        {
                webHostBuilder.ConfigureTestServices(services =>
                {
                // Replace ApplicationDbContext with InMemory for isolation
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                });

                // Add test auth scheme
                services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = "Test";
                    opts.DefaultChallengeScheme = "Test";
                    opts.DefaultScheme = "Test";
                });

                // Replace file-based logging with console logging for tests to avoid file locks/null paths
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                });
            });
        });

        var host = base.CreateHost(builder);

        // Ensure DB is created
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        // Seed Identity roles used by the application to avoid missing-role errors during tests
        try
        {
            var roleManager = scope.ServiceProvider.GetService<Microsoft.AspNetCore.Identity.RoleManager<POS_System.Data.Identity.ApplicationRole>>();
            if (roleManager is not null)
            {
                var roles = new[] { "None", "Super admin", "Service provider", "Cashier", "Owner" };
                foreach (var roleName in roles)
                {
                    var existing = roleManager.FindByNameAsync(roleName).GetAwaiter().GetResult();
                    if (existing is null)
                    {
                        roleManager.CreateAsync(new POS_System.Data.Identity.ApplicationRole { Name = roleName }).GetAwaiter().GetResult();
                    }
                }
            }
        }
        catch
        {
            // swallow seeding errors to avoid failing test host startup; tests will surface role-related failures explicitly
        }

        return host;
    }
}
