using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using POS_System.Business.Utils;
using POS_System.Data.Database;
using POS_System.Data.Identity;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace POS_System.Integration.Tests.Helpers;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POSJwtSecretKey"] = JwtTokenHelper.SecretKey,
                ["POSIssuer"] = JwtTokenHelper.Issuer,
                ["POSAudience"] = JwtTokenHelper.Audience,
                ["ConnectionStrings:LocalConnection"] = "Host=localhost;Port=5432;Username=postgres;Password=test;Database=testdb;",
                ["Stripe:SecretKey"] = "INTEGRATION_TEST_STRIPE_PLACEHOLDER_SECRET",
                ["Stripe:PublicKey"] = "INTEGRATION_TEST_STRIPE_PLACEHOLDER_PUBLIC",
                ["EmailConfiguration:From"] = "test@test.com",
                ["EmailConfiguration:SmtpServer"] = "smtp.test.com",
                ["EmailConfiguration:Port"] = "465",
                ["EmailConfiguration:UserName"] = "test@test.com",
                ["EmailConfiguration:Password"] = "testpassword",
                ["BusinessDetails:FileName"] = "test_business_details.json",
                ["BusinessDetails:RelativePath"] = Path.GetTempPath() + Path.DirectorySeparatorChar,
                ["AWS:AccessKeyId"] = "testkey",
                ["AWS:SecretAccessKey"] = "testsecret",
                ["AWS:Region"] = "eu-north-1",
                ["AWS:SessionToken"] = "testtoken",
                ["FileProvider:Events:Path"] = "Logs/Events/events.log",
                ["FileProvider:Events:FileCreationInterval"] = "12:00:00",
                ["FileProvider:Exceptions:Path"] = "Logs/Exceptions/exceptions.log",
                ["FileProvider:Exceptions:FileCreationInterval"] = "24:00:00",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender, FakeEmailSender>();
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await SeedRolesAsync(roleManager);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        var allClaims = new[]
        {
            "ItemRead", "ItemWrite", "ServiceRead", "ServiceWrite",
            "TransactionRead", "TransactionWrite", "EmployeesRead", "EmployeesWrite",
            "TaxRead", "TaxWrite", "GiftCardRead", "GiftCardWrite",
            "CartItemRead", "CartItemWrite", "ItemDiscountRead", "ItemDiscountWrite",
            "BusinessDetailsRead", "BusinessDetailsWrite", "HistoricRead", "HistoricWrite",
            "HistoricTransactionRead", "HistoricTransactionWrite"
        };

        var roleDefinitions = new (string Name, string[] Claims)[]
        {
            ("Super admin", allClaims),
            ("Service provider", Array.Empty<string>()),
            ("Cashier", Array.Empty<string>()),
            ("Owner", Array.Empty<string>()),
            ("None", Array.Empty<string>()),
        };

        foreach (var (name, claims) in roleDefinitions)
        {
            if (!await roleManager.RoleExistsAsync(name))
            {
                var role = new ApplicationRole { Name = name };
                await roleManager.CreateAsync(role);
                foreach (var claim in claims)
                    await roleManager.AddClaimAsync(role, new Claim(claim, "Y"));
            }
        }
    }

    public HttpClient CreateClientWithAllClaims()
    {
        var client = CreateClient();
        var token = JwtTokenHelper.GenerateToken(
            "ItemRead", "ItemWrite", "ServiceRead", "ServiceWrite",
            "TransactionRead", "TransactionWrite", "EmployeesRead", "EmployeesWrite",
            "TaxRead", "TaxWrite", "GiftCardRead", "GiftCardWrite",
            "CartItemRead", "CartItemWrite", "ItemDiscountRead", "ItemDiscountWrite",
            "BusinessDetailsRead", "BusinessDetailsWrite"
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateClientWithClaims(params string[] claimNames)
    {
        var client = CreateClient();
        var token = JwtTokenHelper.GenerateToken(claimNames);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
