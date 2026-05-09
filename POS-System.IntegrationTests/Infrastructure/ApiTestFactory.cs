using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS_System.Api;
using POS_System.Data.Database;
using POS_System.Data.Identity;
using POS_System.Domain.Entities;
using POS_System.Common.Enums;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Linq;
using Xunit;

namespace POS_System.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory for integration testing with in-memory database.
/// Configures real services except for third-party integrations.
/// </summary>

public sealed class ApiTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string InMemoryDatabaseName = "POSTestDb";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POSJwtSecretKey"] = "IntegrationTestsSecretKey_12345678901234567890",
                ["POSIssuer"] = "https://integration-tests.local",
                ["POSAudience"] = "https://integration-tests.local",
                ["Stripe:SecretKey"] = "sk_test_integration_tests",
                ["EmailConfiguration:From"] = "integration-tests@example.com",
                ["EmailConfiguration:SmtpServer"] = "localhost",
                ["EmailConfiguration:Port"] = "25",
                ["EmailConfiguration:UserName"] = "integration-tests@example.com",
                ["EmailConfiguration:Password"] = "password",
                ["ConnectionStrings:LocalConnection"] = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=integration_tests",
                ["BusinessDetails:FileName"] = "business-details.json",
                ["BusinessDetails:RelativePath"] = "./",
            });
        });
        
        builder.ConfigureTestServices(services =>
        {
            // Remove PostgreSQL DbContext and options
            var dbContextDescriptors = services.Where(d => 
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(ApplicationDbContext)).ToList();
            
            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(InMemoryDatabaseName), ServiceLifetime.Scoped);

            // Add test authentication
            services.AddAuthentication("Test")
                .AddScheme<TestAuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed baseline entities for integration tests
        if (!dbContext.Users.Any())
        {
            var user = new ApplicationUser
            {
                Id = 1,
                UserName = "TestEmployee1",
                Email = "test@example.com",
                EmployeeId = 1,
                FirstName = "Test",
                LastName = "Employee",
                RoleId = 1
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        if (!dbContext.Set<Tax>().Any())
        {
            var tax = new Tax
            {
                Id = 1,
                TaxId = 1,
                Name = "Test Tax",
                Rate = 10,
                IsPercentage = true,
                Version = DateTime.UtcNow,
                IsDeleted = false
            };
            dbContext.Set<Tax>().Add(tax);
            await dbContext.SaveChangesAsync();
        }

        if (!dbContext.Set<Cart>().Any())
        {
            var cart = new Cart
            {
                Id = 1,
                EmployeeVersionId = 1,
                DateCreated = DateTime.UtcNow,
                IsDeleted = false,
                Status = CartStatusEnum.PENDING
            };
            dbContext.Set<Cart>().Add(cart);
            await dbContext.SaveChangesAsync();
        }

        if (!dbContext.Set<TimeSlot>().Any())
        {
            var timeSlot = new TimeSlot
            {
                Id = 1,
                EmployeeVersionId = 1,
                StartTime = DateTime.UtcNow.AddHours(2),
                IsAvailable = true
            };
            dbContext.Set<TimeSlot>().Add(timeSlot);
            await dbContext.SaveChangesAsync();
        }

        if (!dbContext.Set<CartItem>().Any())
        {
            var cartItem = new CartItem
            {
                Id = 1,
                CartId = 1,
                Quantity = 1,
                IsProduct = true,
                IsDeleted = false
            };
            dbContext.Set<CartItem>().Add(cartItem);
            await dbContext.SaveChangesAsync();
        }

        if (!dbContext.Set<Product>().Any())
        {
            var product = new Product
            {
                Id = 1,
                ProductId = 1,
                Name = "Test Product",
                Description = "A test product for integration tests",
                Price = 5000,
                ImageURL = "https://example.com/test-product.jpg",
                Stock = 100,
                Version = DateTime.UtcNow,
                IsDeleted = false
            };
            dbContext.Set<Product>().Add(product);
            await dbContext.SaveChangesAsync();
        }

        if (!dbContext.Set<Service>().Any())
        {
            var service = new Service
            {
                Id = 1,
                ServiceId = 1,
                Name = "Test Service",
                Description = "A test service for integration tests",
                Duration = 60,
                Price = 10000,
                ImageURL = "https://example.com/test-service.jpg",
                EmployeeId = 1,
                Version = DateTime.UtcNow,
                IsDeleted = false
            };
            dbContext.Set<Service>().Add(service);
            await dbContext.SaveChangesAsync();
        }

        if (!dbContext.Set<ItemDiscount>().Any())
        {
            var discount = new ItemDiscount
            {
                Id = 1,
                ItemDiscountId = 1,
                Value = 10,
                IsPercentage = true,
                Description = "Test 10% discount",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                Version = DateTime.UtcNow,
                IsDeleted = false
            };
            dbContext.Set<ItemDiscount>().Add(discount);
            await dbContext.SaveChangesAsync();
        }

        if (!dbContext.Set<GiftCard>().Any())
        {
            var giftCard = new GiftCard
            {
                Id = "TEST-GIFT-CARD-001",
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Value = 5000
            };
            dbContext.Set<GiftCard>().Add(giftCard);
            await dbContext.SaveChangesAsync();
        }

    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        
        await base.DisposeAsync();
    }

    /// <summary>
    /// Creates an authenticated HTTP client with specified claims.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(params string[] claims)
    {
        var client = CreateClient();
        var claimList = claims.Select(c => new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", c))
            .ToList();
        
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", 
                string.Join(",", claimList.Select(c => c.Value)));
        
        return client;
    }

    /// <summary>
    /// Gets a scoped database context for direct database assertions.
    /// </summary>
    public ApplicationDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}

/// <summary>
/// Test authentication scheme for injecting custom claims.
/// </summary>
public class TestAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
}

public sealed class TestAuthenticationHandler : AuthenticationHandler<TestAuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<TestAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>();
        
        if (!string.IsNullOrEmpty(authHeader))
        {
            var claimValues = authHeader.Replace("Test ", string.Empty).Split(',');
            foreach (var claimValue in claimValues)
            {
                claims.Add(new Claim(ClaimTypes.Role, claimValue.Trim()));
                claims.Add(new Claim(claimValue.Trim(), claimValue.Trim()));
            }
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Test collection definition for sharing factory across tests.
/// </summary>
[CollectionDefinition(nameof(ApiTestCollection))]
public sealed class ApiTestCollection : ICollectionFixture<ApiTestFactory>
{
}
