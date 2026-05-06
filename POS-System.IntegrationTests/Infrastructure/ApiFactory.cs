using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using POS_System.Data.Database;
using POS_System.Data.Identity;
using Testcontainers.PostgreSql;

namespace POS_System.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the full POS API against a disposable Postgres container.
/// Only third-party dependencies (email) are mocked.
/// One container and one host is shared per test collection.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("pos_tests")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POSJwtSecretKey"] = "TestSecretKey_ForPosSystem_MustBe32CharsLong!",
                ["POSIssuer"] = "https://test-issuer.com",
                ["POSAudience"] = "https://test-audience.com",
                ["Stripe:SecretKey"] = "sk_test_fake_key_for_integration_tests",
                ["Stripe:PublicKey"] = "pk_test_fake_key_for_integration_tests",
                ["EmailConfiguration:From"] = "test@test.com",
                ["EmailConfiguration:SmtpServer"] = "localhost",
                ["EmailConfiguration:Port"] = "25",
                ["EmailConfiguration:UserName"] = "testuser",
                ["EmailConfiguration:Password"] = "testpassword",
                ["AWS:AccessKeyId"] = "fakeAccessKeyId",
                ["AWS:SecretAccessKey"] = "fakeSecretAccessKey",
                ["AWS:SessionToken"] = "fakeSessionToken",
                ["AWS:Region"] = "eu-north-1",
                ["FileProvider:Events:Path"] = Path.Combine(Path.GetTempPath(), "pos-test-events.log"),
                ["FileProvider:Events:FileCreationInterval"] = "12:00:00",
                ["FileProvider:Exceptions:Path"] = Path.Combine(Path.GetTempPath(), "pos-test-exceptions.log"),
                ["FileProvider:Exceptions:FileCreationInterval"] = "24:00:00",
                ["BusinessDetails:FileName"] = "pos-test-business-details.json",
                ["BusinessDetails:RelativePath"] = Path.GetTempPath(),
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Swap EF Core to the test container
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o =>
                o.UseNpgsql(_db.GetConnectionString(),
                    npgsql => npgsql.MigrationsAssembly("POS-System.Data")));

            // Replace JWT auth with the test authentication handler
            services.Configure<AuthenticationOptions>(opts =>
            {
                opts.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                opts.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                opts.DefaultScheme = TestAuthenticationHandler.SchemeName;
            });
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName, _ => { });

            // Mock email sender to avoid real SMTP connections
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender>(_ => Mock.Of<IEmailSender>());

            // Replace Stripe HTTP client with an in-memory fake to avoid real API calls
            Stripe.StripeConfiguration.StripeClient = new FakeStripeClient();
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

        // Seed ASP.NET Identity roles required by EmployeeService.UpdateEmployeeByIdAsync
        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var roleName in new[] { "None", "Service provider", "Cashier", "Owner", "Super admin" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    public ApplicationDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>Creates an HTTP client authenticated with all policy claims.</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", "Admin");
        return client;
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }
