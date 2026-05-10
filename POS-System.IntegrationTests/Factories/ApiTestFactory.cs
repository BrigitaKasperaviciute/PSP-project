using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using POS_System.Data.Database;
using POS_System.Domain.Entities;

namespace POS_System.IntegrationTests.Factories
{
    /// <summary>
    /// Factory for creating HTTP clients and managing test infrastructure.
    /// Configures WebApplicationFactory with in-memory database and test authentication.
    /// </summary>
    public class ApiTestFactory : WebApplicationFactory<Program>
    {
        private const string InMemoryDatabaseName = "TestDatabase";
        private const string TestJwtSecret = "IntegrationTestsSecretKey_12345678901234567890";
        private const string TestIssuer = "https://integration-tests.local";
        private const string TestAudience = "https://integration-tests.local";
        private readonly string _databaseName = InMemoryDatabaseName + Guid.NewGuid();

        public string BusinessDetailsFilePath { get; private set; } = string.Empty;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var businessDetailsDirectory = Path.Combine(Path.GetTempPath(), "pos-system-integration-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(businessDetailsDirectory);

            BusinessDetailsFilePath = Path.Combine(businessDetailsDirectory, "business_details.json");
            var seededBusinessDetails = new BusinessDetails
            {
                BusinessName = "Seeded Business",
                BusinessEmail = "seeded@example.com",
                BusinessPhone = "+37060000000",
                Country = "Lithuania",
                City = "Vilnius",
                Street = "Main Street",
                HouseNumber = 1,
                FlatNumber = null
            };

            File.WriteAllText(BusinessDetailsFilePath, System.Text.Json.JsonSerializer.Serialize(seededBusinessDetails));

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "POSJwtSecretKey", TestJwtSecret },
                    { "POSIssuer", TestIssuer },
                    { "POSAudience", TestAudience },
                    { "Stripe:SecretKey", "sk_test_integration_tests" },
                    { "EmailConfiguration:From", "integration-tests@example.com" },
                    { "EmailConfiguration:SmtpServer", "localhost" },
                    { "EmailConfiguration:Port", "25" },
                    { "BusinessDetails:FileName", "business_details.json" },
                    { "BusinessDetails:RelativePath", businessDetailsDirectory + Path.DirectorySeparatorChar }
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                if (dbContextDescriptor != null)
                    services.Remove(dbContextDescriptor);

                // Add in-memory database
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                });

                // Create a service provider and initialize the database
                var serviceProvider = services.BuildServiceProvider();
                using (var scope = serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    dbContext.Database.EnsureCreated();

                    // Seed test data
                    SeedTestData(dbContext);
                }

                services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                });
            });
        }

        /// <summary>
        /// Creates an authenticated HTTP client with all claims for admin access.
        /// </summary>
        public async Task<HttpClient> CreateAuthenticatedClientAsync()
        {
            var client = CreateClient();
            var token = GenerateJwtToken(new[] 
            { 
                new Claim("TransactionWrite", "true"),
                new Claim("TransactionRead", "true"),
                new Claim("HistoricTransactionWrite", "true"),
                new Claim("HistoricTransactionRead", "true"),
                new Claim("ServiceWrite", "true"),
                new Claim("ServiceRead", "true"),
                new Claim("ItemWrite", "true"),
                new Claim("ItemRead", "true"),
                new Claim("EmployeesWrite", "true"),
                new Claim("EmployeesRead", "true"),
                new Claim("TaxWrite", "true"),
                new Claim("TaxRead", "true"),
                new Claim("HistoricWrite", "true"),
                new Claim("HistoricRead", "true"),
                new Claim("GiftCardWrite", "true"),
                new Claim("GiftCardRead", "true"),
                new Claim("CartItemWrite", "true"),
                new Claim("CartItemRead", "true"),
                new Claim("ItemDiscountRead", "true"),
                new Claim("ItemDiscountWrite", "true"),
                new Claim("BusinessDetailsRead", "true"),
                new Claim("BusinessDetailsWrite", "true"),
            });

            client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return await Task.FromResult(client);
        }

        /// <summary>
        /// Creates an HTTP client with limited claims for testing authorization.
        /// </summary>
        public async Task<HttpClient> CreateClientWithClaimsAsync(params Claim[] claims)
        {
            var client = CreateClient();
            var token = GenerateJwtToken(claims);
            client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return await Task.FromResult(client);
        }

        /// <summary>
        /// Creates an unauthenticated HTTP client for testing public endpoints.
        /// </summary>
        public HttpClient CreateUnauthenticatedClient()
        {
            return CreateClient();
        }

        /// <summary>
        /// Generates a JWT token with the specified claims.
        /// </summary>
        private string GenerateJwtToken(IEnumerable<Claim> claims)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: TestIssuer,
                audience: TestAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Seeds test data into the in-memory database.
        /// </summary>
        private static void SeedTestData(ApplicationDbContext dbContext)
        {
            // Seed will be called from individual tests as needed
            // to ensure test isolation
        }

        private sealed class TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
            public const string SchemeName = "TestAuth";

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (!Request.Headers.ContainsKey("Authorization"))
                {
                    return Task.FromResult(AuthenticateResult.NoResult());
                }

                var identity = new ClaimsIdentity(GetAuthorizedClaims(), SchemeName);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, SchemeName);

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            private static IEnumerable<Claim> GetAuthorizedClaims()
            {
                return new[]
                {
                    new Claim("TransactionWrite", "true"),
                    new Claim("TransactionRead", "true"),
                    new Claim("HistoricTransactionWrite", "true"),
                    new Claim("HistoricTransactionRead", "true"),
                    new Claim("ServiceWrite", "true"),
                    new Claim("ServiceRead", "true"),
                    new Claim("ItemWrite", "true"),
                    new Claim("ItemRead", "true"),
                    new Claim("EmployeesWrite", "true"),
                    new Claim("EmployeesRead", "true"),
                    new Claim("TaxWrite", "true"),
                    new Claim("TaxRead", "true"),
                    new Claim("HistoricWrite", "true"),
                    new Claim("HistoricRead", "true"),
                    new Claim("GiftCardWrite", "true"),
                    new Claim("GiftCardRead", "true"),
                    new Claim("CartItemWrite", "true"),
                    new Claim("CartItemRead", "true"),
                    new Claim("ItemDiscountRead", "true"),
                    new Claim("ItemDiscountWrite", "true"),
                    new Claim("BusinessDetailsRead", "true"),
                    new Claim("BusinessDetailsWrite", "true")
                };
            }
        }
    }
}
