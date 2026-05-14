using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using POS_System.Data.Database;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using POS_System.Data.Identity;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace POS_System.IntegrationTests.Infrastructure;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    private class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string HeaderName = "X-Test-Claims";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
            UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var header))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim> { new Claim(ClaimTypes.Name, "integration-test-user") };
            var parts = header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                claims.Add(new Claim(p, "true"));
            }

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public async Task<T> ExecuteDbContextAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    public HttpClient CreateAuthenticatedClient(IEnumerable<string>? claims = null)
    {
        var client = CreateClient();
        var headerValue = claims is null ? string.Empty : string.Join(',', claims);
        client.DefaultRequestHeaders.Remove("X-Test-Claims");
        client.DefaultRequestHeaders.Add("X-Test-Claims", headerValue);
        return client;
    }

    public HttpClient CreateClientWithoutClaims()
    {
        return CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var businessDetailsDirectory = Path.Combine(Path.GetTempPath(), "POS-System.IntegrationTests", Guid.NewGuid().ToString("N"), "BusinessDetails");
            Directory.CreateDirectory(businessDetailsDirectory);

            var businessDetailsFile = Path.Combine(businessDetailsDirectory, "business-details.json");
            if (!File.Exists(businessDetailsFile))
            {
                File.WriteAllText(
                    businessDetailsFile,
                    "{\"BusinessName\":\"Integration Tests\",\"BusinessEmail\":\"integration@example.com\",\"BusinessPhone\":\"+37060000000\",\"Country\":\"LT\",\"City\":\"Vilnius\",\"Street\":\"Test Street\",\"HouseNumber\":1,\"FlatNumber\":null}"
                );
            }

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BusinessDetails:FileName"] = "business-details.json",
                ["BusinessDetails:RelativePath"] = businessDetailsDirectory + Path.DirectorySeparatorChar
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace authentication with test handler
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            // Replace DbContext with in-memory Sqlite
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Ensure DB is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var roleNames = new[] { "Super admin", "Service provider", "Cashier", "Owner", "None" };
            foreach (var roleName in roleNames)
            {
                if (roleManager.Roles.Any(role => role.Name == roleName))
                {
                    continue;
                }

                roleManager.CreateAsync(new ApplicationRole { Name = roleName }).GetAwaiter().GetResult();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}
