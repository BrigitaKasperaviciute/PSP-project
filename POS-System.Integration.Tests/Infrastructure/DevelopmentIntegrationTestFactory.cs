using Microsoft.AspNetCore.Hosting;

namespace POS_System.Integration.Tests.Infrastructure;

// A variant of IntegrationTestFactory that sets the environment to Development so
// Swagger middleware is enabled (app.UseSwagger / app.UseSwaggerUI).
public class DevelopmentIntegrationTestFactory : IntegrationTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Development");
    }
}
