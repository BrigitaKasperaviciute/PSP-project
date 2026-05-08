using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class ServiceControllerIntegrationTests
{
    [Fact]
    public async Task CreateService_ValidClaimAndPayload_PersistsServiceAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["ServiceWrite"]);

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Services.CountAsync());
        var payload = new
        {
            name = "Integration Service",
            description = "Integration test service",
            duration = 30,
            price = 990,
            imageURL = "https://example.com/service.png",
            employeeId = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/services", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("name").GetString().Should().Be("Integration Service");

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Services.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateService_MissingClaim_ReturnsForbiddenAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Services.CountAsync());
        var payload = new
        {
            name = "Blocked Service",
            description = "Blocked",
            duration = 10,
            price = 100,
            imageURL = "https://example.com/service.png",
            employeeId = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/services", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Services.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
