using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using POS_System.Business.Dtos;
using POS_System.Business.Utils;
using POS_System.Data.Database;
using POS_System.Data.Identity;
using Xunit;
using System.Data.Common;

namespace POS_System.IntegrationTests.TestSupport;

public sealed class ApiTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string BusinessDetailsFileName = "business-details.json";
    private readonly SqliteConnection _connection;
    private readonly string _businessDetailsDirectory;

    public ApiTestFactory()
    {
        Environment.SetEnvironmentVariable("DATABASE_URL", "Host=localhost;Database=integration_tests;Username=postgres;Password=postgres");
        Environment.SetEnvironmentVariable("POSJwtSecretKey", "integration-tests-secret-key-integration-tests-secret-key");
        Environment.SetEnvironmentVariable("POSIssuer", "IntegrationTests");
        Environment.SetEnvironmentVariable("POSAudience", "IntegrationTests");
        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_integration");
        Environment.SetEnvironmentVariable("Stripe__PublicKey", "pk_test_integration");
        Environment.SetEnvironmentVariable("EmailConfiguration__From", "noreply@example.com");
        Environment.SetEnvironmentVariable("EmailConfiguration__SmtpServer", "smtp.example.com");
        Environment.SetEnvironmentVariable("EmailConfiguration__Port", "25");
        Environment.SetEnvironmentVariable("EmailConfiguration__UserName", "smtp-user");
        Environment.SetEnvironmentVariable("EmailConfiguration__Password", "smtp-password");

        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _businessDetailsDirectory = Path.Combine(Path.GetTempPath(), "POS-System.IntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_businessDetailsDirectory);
        Environment.SetEnvironmentVariable("BusinessDetails__FileName", BusinessDetailsFileName);
        Environment.SetEnvironmentVariable("BusinessDetails__RelativePath", _businessDetailsDirectory + Path.DirectorySeparatorChar);
        File.WriteAllText(GetBusinessDetailsFilePath(), "{\"BusinessName\":\"Test Business\",\"BusinessEmail\":\"business@example.com\",\"BusinessPhone\":\"+1234567890\",\"Country\":\"Country\",\"City\":\"City\",\"Street\":\"Street\",\"HouseNumber\":1,\"FlatNumber\":null}");
    }

    public string GetBusinessDetailsFilePath() => Path.Combine(_businessDetailsDirectory, BusinessDetailsFileName);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POSJwtSecretKey"] = "integration-tests-secret-key-integration-tests-secret-key",
                ["POSIssuer"] = "IntegrationTests",
                ["POSAudience"] = "IntegrationTests",
                ["Stripe:SecretKey"] = "sk_test_integration",
                ["Stripe:PublicKey"] = "pk_test_integration",
                ["EmailConfiguration:From"] = "noreply@example.com",
                ["EmailConfiguration:SmtpServer"] = "smtp.example.com",
                ["EmailConfiguration:Port"] = "25",
                ["EmailConfiguration:UserName"] = "smtp-user",
                ["EmailConfiguration:Password"] = "smtp-password",
                ["BusinessDetails:FileName"] = BusinessDetailsFileName,
                ["BusinessDetails:RelativePath"] = _businessDetailsDirectory + Path.DirectorySeparatorChar
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbConnection>();
            services.RemoveAll<IEmailSender>();

            services.AddSingleton<DbConnection>(_connection);
            services.AddDbContext<ApplicationDbContext>((sp, options) => options.UseSqlite(sp.GetRequiredService<DbConnection>()));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.AddSingleton<IEmailSender, NoOpEmailSender>();
        });
    }

    public HttpClient CreateAuthenticatedClient(params string[] claims)
    {
        var client = CreateClient();
        if (claims.Length > 0)
        {
            client.DefaultRequestHeaders.Add("X-Test-Claims", string.Join(',', claims));
        }
        return client;
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var roleName in new[] { "Super admin", "Service provider", "Cashier", "Owner", "None" })
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }
    }

    public Task InitializeAsync() => ResetDatabaseAsync();

    public new async Task DisposeAsync()
    {
        _connection.Dispose();
        try
        {
            if (Directory.Exists(_businessDetailsDirectory))
            {
                Directory.Delete(_businessDetailsDirectory, true);
            }
        }
        catch
        {
        }

        await Task.CompletedTask;
    }

    private sealed class NoOpEmailSender : IEmailSender
    {
        public Task SendAsync(Message message) => Task.CompletedTask;
    }
}
