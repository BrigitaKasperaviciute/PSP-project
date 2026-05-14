using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS_System.Api;
using POS_System.Business.Logger;
using POS_System.Data.Database;
using POS_System.Data.Identity;
using Xunit;

namespace POS_System.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory that bootstraps the real API with an in-memory SQLite database.
/// Only third-party dependencies are mocked; all application components are real.
/// Lifetime: One database per test class collection.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Cache=Shared");

    public ApiTestFactory()
    {
    Environment.SetEnvironmentVariable("DATABASE_URL", "Data Source=:memory:");
    Environment.SetEnvironmentVariable("POSJwtSecretKey", "IntegrationTestsSecretKey_12345678901234567890");
    Environment.SetEnvironmentVariable("POSIssuer", "https://integration-tests.local");
    Environment.SetEnvironmentVariable("POSAudience", "https://integration-tests.local");
    Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_integration_tests");
    Environment.SetEnvironmentVariable("Stripe__PublicKey", "pk_test_integration_tests");
    Environment.SetEnvironmentVariable("EmailConfiguration__From", "integration-tests@example.com");
    Environment.SetEnvironmentVariable("EmailConfiguration__SmtpServer", "localhost");
    Environment.SetEnvironmentVariable("EmailConfiguration__Port", "25");
    Environment.SetEnvironmentVariable("EmailConfiguration__UserName", "integration-tests@example.com");
    Environment.SetEnvironmentVariable("EmailConfiguration__Password", "password");
    Environment.SetEnvironmentVariable("AWS__AccessKeyId", "integration-tests");
    Environment.SetEnvironmentVariable("AWS__SecretAccessKey", "integration-tests");
    Environment.SetEnvironmentVariable("AWS__SessionToken", "integration-tests");
    Environment.SetEnvironmentVariable("AWS__Region", "eu-north-1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LocalConnection"] = "Data Source=:memory:",
                ["POSJwtSecretKey"] = "IntegrationTestsSecretKey_12345678901234567890",
                ["POSIssuer"] = "https://integration-tests.local",
                ["POSAudience"] = "https://integration-tests.local",
                ["Stripe:SecretKey"] = "sk_test_integration_tests",
                ["Stripe:PublicKey"] = "pk_test_integration_tests",
                ["EmailConfiguration:From"] = "integration-tests@example.com",
                ["EmailConfiguration:SmtpServer"] = "localhost",
                ["EmailConfiguration:Port"] = "25",
                ["EmailConfiguration:UserName"] = "integration-tests@example.com",
                ["EmailConfiguration:Password"] = "password",
                ["AWS:AccessKeyId"] = "integration-tests",
                ["AWS:SecretAccessKey"] = "integration-tests",
                ["AWS:SessionToken"] = "integration-tests",
                ["AWS:Region"] = "eu-north-1",
                // Empty FileProvider to avoid file logging issues in tests
                ["FileProvider:Events:Path"] = "",
                ["FileProvider:Exceptions:Path"] = "",
                // BusinessDetails configuration for test factory
                ["BusinessDetails:FileName"] = "business_details.json",
                ["BusinessDetails:RelativePath"] = ""
            });
        });
        builder.ConfigureTestServices(services =>
        {
            // Replace the database with an in-memory SQLite connection that stays open for the test run.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddSingleton(_connection);
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseSqlite(sp.GetRequiredService<SqliteConnection>());
            });
            services.AddDbContextFactory<ApplicationDbContext>((sp, options) =>
            {
                options.UseSqlite(sp.GetRequiredService<SqliteConnection>());
            });

            // Override logging to use console instead of file logger
            services.RemoveAll<ILoggerProvider>();
            services.RemoveAll(typeof(IConfigureOptions<>).MakeGenericType(typeof(ApplicationLoggerOptions)));
            services.AddLogging(options =>
            {
                options.ClearProviders();
                options.AddConsole();
            });

            // Replace real coupon service with fake test implementation to avoid external Stripe calls
            services.RemoveAll<POS_System.Business.Services.Interfaces.ICouponService>();
            services.AddScoped<POS_System.Business.Services.Interfaces.ICouponService, POS_System.IntegrationTests.Infrastructure.MockServices.FakeCouponService>();

            // Add test authentication
            services.AddTestAuthentication();
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        // Create temporary business details file for the repository
        string businessDetailsPath = "business_details.json";
        if (!File.Exists(businessDetailsPath))
        {
            File.WriteAllText(businessDetailsPath, "{}");
        }

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Create tables
        await dbContext.Database.EnsureCreatedAsync();
        
        // Seed initial data (roles, etc.)
        await SeedInitialDataAsync(dbContext);
    }

    public new async Task DisposeAsync()
    {
        // Clean up temporary business details file
        string businessDetailsPath = "business_details.json";
        if (File.Exists(businessDetailsPath))
        {
            try { File.Delete(businessDetailsPath); }
            catch { /* Ignore cleanup errors */ }
        }

        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Creates a new database context for assertions
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        return Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        await SeedInitialDataAsync(context);
    }

    /// <summary>
    /// Creates an authenticated HTTP client with specific role/claims
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string? role = "Admin", int? userId = null)
    {
        var client = CreateClient();
        var claims = new Dictionary<string, string>
        {
            { "sub", (userId ?? 1).ToString() },
            { "role", role ?? "User" },
            { "email", $"test-{Guid.NewGuid():N}@example.com" }
        };

        client.DefaultRequestHeaders.Add("X-Test-Claims", System.Text.Json.JsonSerializer.Serialize(claims));
        return client;
    }

    private static async Task SeedInitialDataAsync(ApplicationDbContext dbContext)
    {
        if (!await dbContext.Roles.AnyAsync())
        {
            var insertSql = $$"""
                INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
                VALUES
                    (0, 'None', 'NONE', '{{Guid.NewGuid()}}'),
                    (1, 'Service provider', 'SERVICE PROVIDER', '{{Guid.NewGuid()}}'),
                    (2, 'Cashier', 'CASHIER', '{{Guid.NewGuid()}}'),
                    (3, 'Owner', 'OWNER', '{{Guid.NewGuid()}}'),
                    (4, 'Super admin', 'SUPER ADMIN', '{{Guid.NewGuid()}}');
                """;

            await dbContext.Database.ExecuteSqlRawAsync(insertSql);
        }
    }
}

/// <summary>
/// Collection fixture definition for test sharing
/// </summary>
[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiTestFactory>
{
}
