using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using POS_System.Business.Services.Interfaces;
using POS_System.Business.Utils;
using POS_System.Data.Database;
using POS_System.Data.Identity;
using Testcontainers.PostgreSql;

namespace POS_System.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("pos_integration_tests")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        Environment.SetEnvironmentVariable("POSJwtSecretKey", "integration-test-secret-key-must-be-at-least-32-chars-1234567890");
        Environment.SetEnvironmentVariable("POSIssuer", "TestIssuer");
        Environment.SetEnvironmentVariable("POSAudience", "TestAudience");
        Environment.SetEnvironmentVariable("EmailConfiguration__From", "test@test.com");
        Environment.SetEnvironmentVariable("EmailConfiguration__SmtpServer", "localhost");
        Environment.SetEnvironmentVariable("EmailConfiguration__Port", "25");
        Environment.SetEnvironmentVariable("EmailConfiguration__UserName", "test");
        Environment.SetEnvironmentVariable("EmailConfiguration__Password", "test");
        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_fake_key_for_integration_testing_only");
        Environment.SetEnvironmentVariable("AWS__Region", "eu-north-1");
        Environment.SetEnvironmentVariable("AWS__AccessKeyId", "test-access-key");
        Environment.SetEnvironmentVariable("AWS__SecretAccessKey", "test-secret-key");
        Environment.SetEnvironmentVariable("AWS__SessionToken", "test-session-token");
        Environment.SetEnvironmentVariable("FileProvider__LogDirectory", "Logs");
        Environment.SetEnvironmentVariable("FileProvider__FileSizeLimit", "1048576");
        Environment.SetEnvironmentVariable("FileProvider__RetainedFileCountLimit", "2");
        Environment.SetEnvironmentVariable("BusinessDetails__FileName", "business-details.json");
        Environment.SetEnvironmentVariable("BusinessDetails__RelativePath", "./");

        builder.ConfigureTestServices(services =>
        {
            // Replace DbContext with the test container connection string.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseNpgsql(_db.GetConnectionString(),
                    npgsql => npgsql.MigrationsAssembly("POS-System.Data")));

            // Replace authentication with the test scheme.
            services.PostConfigure<AuthenticationOptions>(opts =>
            {
                opts.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                opts.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                opts.DefaultScheme = TestAuthHandler.SchemeName;
            });
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Mock third-party: email sender and payment service.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, FakeEmailSender>();

            services.RemoveAll<IPaymentService>();
            services.AddSingleton<IPaymentService, FakePaymentService>();
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        // Seed Identity roles required by registration flow.
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        string[] roles = ["Super admin", "Owner", "Cashier", "Service provider", "None"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
        }
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    public ApplicationDbContext CreateDbContext() =>
        Services.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();

    /// <summary>Returns an HTTP client with the specified claims injected via X-Test-Claims header.</summary>
    public HttpClient CreateClientWithClaims(params string[] claims)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ClaimsHeader, string.Join(",", claims));
        return client;
    }

    /// <summary>Returns an HTTP client with all system claims — simulates a super-admin.</summary>
    public HttpClient CreateAdminClient() => CreateClientWithClaims(
        "TransactionWrite", "TransactionRead",
        "HistoricTransactionWrite", "HistoricTransactionRead",
        "ServiceWrite", "ServiceRead",
        "ItemWrite", "ItemRead",
        "EmployeesWrite", "EmployeesRead",
        "TaxWrite", "TaxRead",
        "HistoricWrite", "HistoricRead",
        "GiftCardWrite", "GiftCardRead",
        "CartItemWrite", "CartItemRead",
        "ItemDiscountRead", "ItemDiscountWrite",
        "BusinessDetailsRead", "BusinessDetailsWrite"
    );
}
