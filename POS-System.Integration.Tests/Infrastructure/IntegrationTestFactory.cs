using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Utils;
using POS_System.Data.Database;
using POS_System.Data.Identity;

namespace POS_System.Integration.Tests.Infrastructure;

public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private bool _initialized;

    // Environment variables are loaded by WebApplication.CreateBuilder before service registration,
    // so they're visible to TryGetConfigValue when AddBusinessServices runs.
    static IntegrationTestFactory()
    {
        Environment.SetEnvironmentVariable("POSJwtSecretKey", "X7#pWq@8!Lm$Yr92&K^Jo4F%AbZ*tNcQ");
        Environment.SetEnvironmentVariable("POSIssuer", "https://auth.POS-System.com");
        Environment.SetEnvironmentVariable("POSAudience", "https://business.com");
        Environment.SetEnvironmentVariable("ConnectionStrings__LocalConnection", "Host=localhost;Port=5432;Database=fake_test");
        Environment.SetEnvironmentVariable("EmailConfiguration__From", "test@test.com");
        Environment.SetEnvironmentVariable("EmailConfiguration__SmtpServer", "localhost");
        Environment.SetEnvironmentVariable("EmailConfiguration__Port", "25");
        Environment.SetEnvironmentVariable("EmailConfiguration__UserName", "test");
        Environment.SetEnvironmentVariable("EmailConfiguration__Password", "test");
        Environment.SetEnvironmentVariable("Stripe__PublicKey", "pk_test_fake");
        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_fakekey");
        Environment.SetEnvironmentVariable("AWS__AccessKeyId", "fakekey");
        Environment.SetEnvironmentVariable("AWS__SecretAccessKey", "fakesecret");
        Environment.SetEnvironmentVariable("AWS__SessionToken", "faketoken");
        Environment.SetEnvironmentVariable("AWS__Region", "eu-north-1");
        Environment.SetEnvironmentVariable("FileProvider__Events__Path", "Logs/Events/events.log");
        Environment.SetEnvironmentVariable("FileProvider__Events__FileCreationInterval", "12:00:00");
        Environment.SetEnvironmentVariable("FileProvider__Exceptions__Path", "Logs/Exceptions/exceptions.log");
        Environment.SetEnvironmentVariable("FileProvider__Exceptions__FileCreationInterval", "24:00:00");
        // BusinessDetailRepository reads these; without them it throws ArgumentNullException("FileName")
        // on every DI scope resolution (breaking all controllers via IUnitOfWork).
        Environment.SetEnvironmentVariable("BusinessDetails__FileName", "business_details.json");
        Environment.SetEnvironmentVariable("BusinessDetails__RelativePath", "TestData/");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove PostgreSQL DbContext options
            var dbContextDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            // Remove ApplicationDbContext registration as well (handles alternate registration)
            var appDbDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(ApplicationDbContext));
            if (appDbDescriptor != null)
                services.Remove(appDbDescriptor);

            // Register in-memory database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Replace IEmailSender with a no-op implementation
            var emailDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IEmailSender));
            if (emailDescriptor != null)
                services.Remove(emailDescriptor);
            services.AddScoped<IEmailSender, NoOpEmailSender>();
        });
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        _initialized = true;

        // BusinessDetailRepository writes to TestData/business_details.json — ensure the directory exists
        Directory.CreateDirectory("TestData");

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await SeedRolesWithClaimsAsync(roleManager);
    }

    private static async Task SeedRolesWithClaimsAsync(RoleManager<ApplicationRole> roleManager)
    {
        var allClaims = new[]
        {
            "TransactionWrite", "TransactionRead", "HistoricTransactionWrite", "HistoricTransactionRead",
            "ServiceWrite", "ServiceRead", "ItemWrite", "ItemRead",
            "EmployeesWrite", "EmployeesRead", "TaxWrite", "TaxRead",
            "GiftCardWrite", "GiftCardRead", "CartItemWrite", "CartItemRead",
            "ItemDiscountRead", "ItemDiscountWrite", "BusinessDetailsRead", "BusinessDetailsWrite"
        };

        var roleNames = new[] { "Super admin", "Service provider", "Cashier", "Owner", "None" };

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new ApplicationRole { Name = roleName };
                await roleManager.CreateAsync(role);

                foreach (var claim in allClaims)
                    await roleManager.AddClaimAsync(role, new Claim(claim, "Y"));
            }
        }
    }

    public HttpClient CreateClientWithAuthToken(params string[] claimNames)
    {
        var token = TestAuthHelper.GenerateToken(claimNames);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<ApplicationUser> CreateTestUserAsync(
        string userName, string password, string roleName = "Super admin")
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByNameAsync(userName);
        if (existing != null)
            return existing;

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@test.com",
            NormalizedEmail = $"{userName}@test.com".ToUpper(),
            FirstName = "Test",
            LastName = "User",
            EmployeeId = new Random().Next(1000, 9999),
            RoleId = 1,
            BirthDate = new DateOnly(1990, 1, 1),
            StartDate = new DateOnly(2020, 1, 1),
            EmailConfirmed = true,
            Version = DateTime.UtcNow,
            IsDeleted = false
        };

        await userManager.CreateAsync(user, password);
        await userManager.AddToRoleAsync(user, roleName);
        return user;
    }
}
