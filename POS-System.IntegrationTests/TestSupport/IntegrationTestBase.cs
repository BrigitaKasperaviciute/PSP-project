using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Data.Database;

namespace POS_System.IntegrationTests.TestSupport;

public abstract class IntegrationTestBase(ApiTestFactory factory)
{
    protected ApiTestFactory Factory { get; } = factory;

    protected HttpClient CreateClient(params string[] claims) => Factory.CreateAuthenticatedClient(claims);

    protected Task ResetDatabaseAsync() => Factory.ResetDatabaseAsync();

    protected async Task<TResult> WithDbContextAsync<TResult>(Func<ApplicationDbContext, Task<TResult>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(context);
    }

    protected async Task WithDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(context);
    }
}
