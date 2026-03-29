using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS_System.Business.Logger;
using POS_System.Business.Utils;
using POS_System.Data.Database;
using POS_System.Data.Identity;

namespace POS_System.IntegrationTests.Infrastructure;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"pos-integration-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POSJwtSecretKey"] = "integration-tests-key-1234567890",
                ["POSIssuer"] = "https://integration-tests.local",
                ["POSAudience"] = "https://integration-tests.local",
                ["Stripe:SecretKey"] = "sk_test_fake",
                ["Stripe:PublicKey"] = "pk_test_fake",
                ["EmailConfiguration:From"] = "tests@example.com",
                ["EmailConfiguration:SmtpServer"] = "localhost",
                ["EmailConfiguration:Port"] = "25",
                ["EmailConfiguration:UserName"] = "tests",
                ["EmailConfiguration:Password"] = "tests",
                ["BusinessDetails:FileName"] = "business-details.integration.json",
                ["BusinessDetails:RelativePath"] = "./",
                ["FileProvider:Events:Path"] = "Logs/events.log",
                ["FileProvider:Events:FileCreationInterval"] = "12:00:00",
                ["FileProvider:Exceptions:Path"] = "Logs/exceptions.log",
                ["FileProvider:Exceptions:FileCreationInterval"] = "24:00:00"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName,
                _ => { });

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, NoopEmailSender>();

            services.RemoveAll<ILoggerProvider>();
            services.RemoveAll<IConfigureOptions<ApplicationLoggerOptions>>();
            services.AddLogging(options => options.ClearProviders());

            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }

            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            dbContext.Database.EnsureCreated();
            EnsureRoles(roleManager).GetAwaiter().GetResult();
        });
    }

    private static async Task EnsureRoles(RoleManager<ApplicationRole> roleManager)
    {
        var roleNames = new[]
        {
            "Super admin",
            "Service provider",
            "Cashier",
            "Owner",
            "None"
        };

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
            }
        }
    }

    private sealed class NoopEmailSender : IEmailSender
    {
        public Task SendAsync(Message message)
        {
            return Task.CompletedTask;
        }
    }
}
