using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using POS_System.Business.Services.Interfaces;
using POS_System.Business.Utils;
using POS_System.Data.Database;
using POS_System.Data.Identity;
using Testcontainers.PostgreSql;
using Xunit;

namespace POS_System.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Environment variables are set before the test host builds so that
    // TryGetConfigValue (which calls Environment.Exit(1) on missing keys)
    // finds all required values during AddBusinessServices registration.
    static ApiFactory()
    {
        Environment.SetEnvironmentVariable("POSJwtSecretKey", "TestSecretKey-32-Characters-Min!X");
        Environment.SetEnvironmentVariable("POSIssuer", "https://test.example.com");
        Environment.SetEnvironmentVariable("POSAudience", "https://test.example.com");
        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_fake_key_for_integration_tests");
        Environment.SetEnvironmentVariable("AWS__AccessKeyId", "fake-access-key");
        Environment.SetEnvironmentVariable("AWS__SecretAccessKey", "fake-secret-key");
        Environment.SetEnvironmentVariable("AWS__SessionToken", "fake-session-token");
        Environment.SetEnvironmentVariable("AWS__Region", "us-east-1");
        Environment.SetEnvironmentVariable("EmailConfiguration__From", "test@test.com");
        Environment.SetEnvironmentVariable("EmailConfiguration__SmtpServer", "localhost");
        Environment.SetEnvironmentVariable("EmailConfiguration__Port", "25");
        Environment.SetEnvironmentVariable("EmailConfiguration__UserName", "testuser");
        Environment.SetEnvironmentVariable("EmailConfiguration__Password", "testpassword");
        Environment.SetEnvironmentVariable("FileProvider__Events__Path", "Logs/Events/events.log");
        Environment.SetEnvironmentVariable("FileProvider__Events__FileCreationInterval", "12:00:00");
        Environment.SetEnvironmentVariable("FileProvider__Exceptions__Path", "Logs/Exceptions/exceptions.log");
        Environment.SetEnvironmentVariable("FileProvider__Exceptions__FileCreationInterval", "24:00:00");
        Environment.SetEnvironmentVariable("ConnectionStrings__LocalConnection", "placeholder");
        Environment.SetEnvironmentVariable("BusinessDetails__FileName", "business_details_test.json");
        Environment.SetEnvironmentVariable("BusinessDetails__RelativePath", Path.GetTempPath());
    }

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("pos_tests")
        .Build();

    public Mock<IEmailSender> EmailSenderMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o =>
                o.UseNpgsql(_db.GetConnectionString(),
                    npgsql => npgsql.MigrationsAssembly("POS-System.Data")));

            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender>(_ => EmailSenderMock.Object);

            services.RemoveAll<ICartDiscountService>();
            services.AddSingleton<ICartDiscountService, FakeCartDiscountService>();

            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        string[] roles = ["None", "Service provider", "Cashier", "Owner", "Super admin"];
        foreach (var roleName in roles)
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

    public HttpClient CreateClientWithClaims(params string[] claims)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                TestAuthHandler.SchemeName, string.Join(",", claims));
        return client;
    }

    public HttpClient CreateAdminClient() => CreateClientWithClaims(
        "TransactionRead", "TransactionWrite",
        "HistoricTransactionRead", "HistoricTransactionWrite",
        "ServiceRead", "ServiceWrite",
        "ItemRead", "ItemWrite",
        "EmployeesRead", "EmployeesWrite",
        "TaxRead", "TaxWrite",
        "GiftCardRead", "GiftCardWrite",
        "CartItemRead", "CartItemWrite",
        "ItemDiscountRead", "ItemDiscountWrite",
        "BusinessDetailsRead", "BusinessDetailsWrite"
    );
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }
