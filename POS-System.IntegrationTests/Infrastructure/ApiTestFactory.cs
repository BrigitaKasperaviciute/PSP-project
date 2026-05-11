using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.Sqlite;
using POS_System.Api;
using POS_System.Data.Database;
using POS_System.Data.Identity;

namespace POS_System.IntegrationTests.Infrastructure;

/// <summary>
/// Factory for spinning up the full API with:
/// - In-memory SQLite database
/// - Test authentication scheme
/// - Full DI container (no mocks for app code)
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    // Ensure critical configuration is available to WebApplication.CreateBuilder
    static ApiTestFactory()
    {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Debug");
        Environment.SetEnvironmentVariable("POSJwtSecretKey", "IntegrationTestsSecretKey_1234567890");
        Environment.SetEnvironmentVariable("POSIssuer", "POS-System-IntegrationTests");
        Environment.SetEnvironmentVariable("POSAudience", "POS-System-IntegrationTests");
        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_integration");
        Environment.SetEnvironmentVariable("Stripe__PublicKey", "pk_test_integration");
        Environment.SetEnvironmentVariable("EmailConfiguration__From", "integration@pos.local");
        Environment.SetEnvironmentVariable("EmailConfiguration__SmtpServer", "localhost");
        Environment.SetEnvironmentVariable("EmailConfiguration__Port", "25");
        Environment.SetEnvironmentVariable("EmailConfiguration__UserName", "test");
        Environment.SetEnvironmentVariable("EmailConfiguration__Password", "test");

        // Create and open a shared in-memory SQLite connection for the lifetime of the test process
        _sqliteConnection = new SqliteConnection("Data Source=:memory:;Mode=Memory;Cache=Shared");
        _sqliteConnection.Open();
    }

    private static SqliteConnection? _sqliteConnection;
    private bool _dbSchemaCreated;
    // Initialization and cleanup are performed by tests when required.

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        // Ensure the application configuration contains the BusinessDetails keys
        var businessDetailsDir = Path.Combine(Path.GetTempPath(), "pos-test");
        Directory.CreateDirectory(businessDetailsDir);
        var businessDetailsFileName = "businessDetails.json";
        var businessDetailsFullPath = Path.Combine(businessDetailsDir, businessDetailsFileName);
        if (!File.Exists(businessDetailsFullPath)) File.WriteAllText(businessDetailsFullPath, "{}");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BusinessDetails:FileName"] = businessDetailsFileName,
                ["BusinessDetails:RelativePath"] = businessDetailsDir + Path.DirectorySeparatorChar,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContext options
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory SQLite database for tests using a shared open connection
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_sqliteConnection!);
            });

            // Add test authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", null);

            // Also configure options for services that consume IOptions<BusinessDetailsConfig>
            services.Configure<BusinessDetailsConfig>(config =>
            {
                config.FileName = businessDetailsFileName;
                config.RelativePath = businessDetailsDir + Path.DirectorySeparatorChar;
            });

            // Register a hosted service to initialize the database schema
            services.AddHostedService<DatabaseInitializer>();
        });
    }

    /// <summary>
    /// Creates an HTTP client with default admin claims.
    /// </summary>
    public HttpClient CreateDefaultClient()
    {
        return CreateClient();
    }

    /// <summary>
    /// Creates an HTTP client with specific claims.
    /// </summary>
    public HttpClient CreateClientWithClaims(params (string Type, string Value)[] claims)
    {
        var client = CreateClient();

        if (claims.Length > 0)
        {
            var claimString = string.Join(";", claims.Select(c => $"{c.Type}:{c.Value}"));
            client.DefaultRequestHeaders.Add("X-Test-Claims", claimString);
        }

        return client;
    }

    /// <summary>
    /// Creates a fresh database context for assertions.
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}

public sealed class BusinessDetailsConfig
{
    public string FileName { get; set; } = "businessDetails.json";
    public string RelativePath { get; set; } = "./";
}

// Database initializer to ensure schema is created
internal sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _services;

    public DatabaseInitializer(IServiceProvider services)
    {
        _services = services;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        // Ensure Identity roles exist for registration/login flows
        var roleManager = scope.ServiceProvider.GetService<Microsoft.AspNetCore.Identity.RoleManager<POS_System.Data.Identity.ApplicationRole>>();
        if (roleManager != null)
        {
            var roles = new[] { "None", "Service provider", "Cashier", "Owner", "Super admin" };
            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new POS_System.Data.Identity.ApplicationRole { Name = roleName, NormalizedName = roleName.ToUpperInvariant() });
                }
            }
        }

        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

// Note: collection definition and fixture wiring are defined in the test project files.
