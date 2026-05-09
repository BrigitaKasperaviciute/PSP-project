using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Data.Database;

namespace POS_System.IntegrationTests.TestSupport;

public static class TestHostExtensions
{
    public static HttpClient CreateClientWithClaims(this CustomWebApplicationFactory factory, params string[] claims)
    {
        var client = factory.CreateClient();

        if (claims.Length > 0)
        {
            client.DefaultRequestHeaders.Add("X-Test-Claims", string.Join(',', claims));
        }

        return client;
    }

    public static void UseDbContext(this CustomWebApplicationFactory factory, Action<ApplicationDbContext> action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        action(db);
    }

    public static T UseDbContext<T>(this CustomWebApplicationFactory factory, Func<ApplicationDbContext, T> action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return action(db);
    }
}