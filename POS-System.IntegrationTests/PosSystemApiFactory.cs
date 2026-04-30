using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using POS_System.Business.Utils;
using POS_System.Data.Database;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests;

public class PosSystemApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    public string TempDirectory { get; }

    public PosSystemApiFactory()
    {
        TempDirectory = Path.Combine(Path.GetTempPath(), "pos-int-" + _dbName);
        Directory.CreateDirectory(TempDirectory);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POSJwtSecretKey"] = JwtTokenHelper.SecretKey,
                ["POSIssuer"] = JwtTokenHelper.Issuer,
                ["POSAudience"] = JwtTokenHelper.Audience,
                ["Stripe:SecretKey"] = "sk_test_fake_key_for_tests",
                ["EmailConfiguration:SmtpServer"] = "localhost",
                ["EmailConfiguration:Port"] = "25",
                ["EmailConfiguration:UserName"] = "test@test.com",
                ["EmailConfiguration:Password"] = "testpassword",
                ["EmailConfiguration:From"] = "test@test.com",
                ["AWS:AccessKeyId"] = "fakeAccessKeyId",
                ["AWS:SecretAccessKey"] = "fakeSecretAccessKey",
                ["AWS:Region"] = "us-east-1",
                ["AWS:SessionToken"] = "fakeSessionToken",
                ["FileProvider:Path"] = Path.Combine(Path.GetTempPath(), "pos-integration-test-logs"),
                ["BusinessDetails:RelativePath"] = TempDirectory + Path.DirectorySeparatorChar,
                ["BusinessDetails:FileName"] = "business-details.json",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing PostgreSQL DbContext registrations
            var dbContextOptionsDescriptor = services
                .SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextOptionsDescriptor != null)
                services.Remove(dbContextOptionsDescriptor);

            var dbContextDescriptor = services
                .SingleOrDefault(d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            // Add InMemory database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Replace real EmailSender with a no-op fake
            var emailDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailSender));
            if (emailDescriptor != null)
                services.Remove(emailDescriptor);
            services.AddScoped<IEmailSender, FakeEmailSender>();

            // Override JWT validation parameters so the test-generated tokens are accepted
            // regardless of what keys were read from appsettings at startup time
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtTokenHelper.SecretKey));
                options.TokenValidationParameters.IssuerSigningKey = key;
                options.TokenValidationParameters.ValidIssuer = JwtTokenHelper.Issuer;
                options.TokenValidationParameters.ValidAudience = JwtTokenHelper.Audience;
            });
        });
    }

    public HttpClient CreateClientWithClaims(params string[] claims)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(claims));
        return client;
    }

    public HttpClient CreateClientWithFullAccess()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateFullAccessToken());
        return client;
    }
}

internal sealed class FakeEmailSender : IEmailSender
{
    public Task SendAsync(Message message) => Task.CompletedTask;
}
