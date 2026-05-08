using POS_System.Business.Logger;
using POS_System.Business.Services.Interfaces;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using POS_System.Data.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace POS_System.IntegrationTests.Infrastructure;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly string[] AllClaims =
    [
        "TransactionWrite",
        "TransactionRead",
        "HistoricTransactionWrite",
        "HistoricTransactionRead",
        "ServiceWrite",
        "ServiceRead",
        "ItemWrite",
        "ItemRead",
        "EmployeesWrite",
        "EmployeesRead",
        "TaxWrite",
        "TaxRead",
        "HistoricWrite",
        "HistoricRead",
        "GiftCardWrite",
        "GiftCardRead",
        "CartItemWrite",
        "CartItemRead",
        "ItemDiscountRead",
        "ItemDiscountWrite",
        "BusinessDetailsRead",
        "BusinessDetailsWrite"
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        var businessDetailsDir = Path.Combine(Path.GetTempPath(), $"pos_business_details_{Guid.NewGuid():N}");
        Directory.CreateDirectory(businessDetailsDir);
        var businessDetailsFileName = "business-details.json";
        var businessDetailsFullPath = Path.Combine(businessDetailsDir, businessDetailsFileName);

        File.WriteAllText(businessDetailsFullPath, """
        {
          "BusinessName": "Bakalaurui PSP",
          "BusinessEmail": "info@example.com",
          "BusinessPhone": "+421900000000",
          "Country": "Slovakia",
          "City": "Bratislava",
          "Street": "Main Street",
          "HouseNumber": 12,
          "FlatNumber": 4
        }
        """);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BusinessDetails:FileName"] = businessDetailsFileName,
                ["BusinessDetails:RelativePath"] = businessDetailsDir + Path.DirectorySeparatorChar,
                ["POSJwtSecretKey"] = "IntegrationTestsSecretKey_12345678901234567890",
                ["POSIssuer"] = "POS-System-IntegrationTests",
                ["POSAudience"] = "POS-System-IntegrationTests",
                ["Stripe:SecretKey"] = "sk_test_integration",
                ["EmailConfiguration:From"] = "integration@pos.local",
                ["EmailConfiguration:SmtpServer"] = "localhost",
                ["EmailConfiguration:Port"] = "25",
                ["EmailConfiguration:UserName"] = "integration",
                ["EmailConfiguration:Password"] = "integration"
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Debug);
        });

        builder.ConfigureTestServices(services =>
        {
            // Configure ApplicationLoggerOptions for tests with valid temp directory paths
            // This prevents null FileName errors when ApplicationLogger tries to write logs
            var logDir = Path.Combine(Path.GetTempPath(), $"pos_test_logs_{Guid.NewGuid():N}");
            Directory.CreateDirectory(logDir);
            services.Configure<ApplicationLoggerOptions>(options =>
            {
                options.Events = new() { Path = Path.Combine(logDir, "events.log"), FileCreationInterval = TimeSpan.FromHours(24) };
                options.Exceptions = new() { Path = Path.Combine(logDir, "exceptions.log"), FileCreationInterval = TimeSpan.FromHours(24) };
            });
            services.RemoveAll(typeof(IStripeCouponService));
            services.AddSingleton<IStripeCouponService, FakeStripeCouponService>();
            // Replace auth with test authentication that grants all claims
            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestAuthenticationHandler.SchemeName;
            });

            // Replace application's Postgres DbContext with a SQLite file DB for tests.
            // This exercises real EF Core/repository/service code without external Postgres dependency.
            var tempDb = Path.Combine(Path.GetTempPath(), $"pos_integration_test_{Guid.NewGuid():N}.db");
            services.RemoveAll(typeof(ApplicationDbContext));
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={tempDb}"));

            // Ensure schema and seed data are initialized when the test host starts.
            services.AddHostedService<EnsureCreatedHostedService>();
        });
    }

    public HttpClient CreateAuthenticatedClient(IEnumerable<string>? claims = null)
    {
        var client = CreateClient();
        var claimList = claims?.ToArray() ?? AllClaims;
        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.ClaimsHeaderName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ClaimsHeaderName, string.Join(',', claimList));
        return client;
    }

    public HttpClient CreateAuthenticatedClient(WebApplicationFactoryClientOptions options, IEnumerable<string>? claims = null)
    {
        var client = CreateClient(options);
        var claimList = claims?.ToArray() ?? AllClaims;
        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.ClaimsHeaderName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ClaimsHeaderName, string.Join(',', claimList));
        return client;
    }

    public HttpClient CreateClientWithoutClaims()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.ClaimsHeaderName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ClaimsHeaderName, string.Empty);
        return client;
    }

    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.ModeHeaderName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ModeHeaderName, TestAuthenticationHandler.AnonymousMode);
        return client;
    }

    public async Task<TResult> ExecuteDbContextAsync<TResult>(Func<ApplicationDbContext, Task<TResult>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    public async Task ExecuteDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(db);
    }
}

public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string ClaimsHeaderName = "X-Test-Claims";
    public const string ModeHeaderName = "X-Test-Auth-Mode";
    public const string AnonymousMode = "anonymous";

    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue(ModeHeaderName, out var mode) &&
            string.Equals(mode.ToString(), AnonymousMode, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Anonymous test mode enabled."));
        }

        var claimsHeader = Request.Headers[ClaimsHeaderName].ToString();

        var configuredClaims = string.IsNullOrWhiteSpace(claimsHeader)
            ? []
            : claimsHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "integration-test-user"),
            new(ClaimTypes.Name, "integration-test-user")
        };

        claims.AddRange(configuredClaims.Select(claim => new Claim(claim, "true")));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

internal sealed class EnsureCreatedHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<EnsureCreatedHostedService> _logger;

    public EnsureCreatedHostedService(IServiceProvider services, ILogger<EnsureCreatedHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ctx.Database.EnsureCreatedAsync(cancellationToken);
            _logger.LogInformation("Created integration test database schema.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed creating integration test database schema.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}