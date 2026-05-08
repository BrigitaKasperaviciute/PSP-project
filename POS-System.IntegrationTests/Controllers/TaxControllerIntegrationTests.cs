using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class TaxControllerIntegrationTests
{
    [Fact]
    public async Task CreateTax_ValidClaimAndPayload_PersistsTaxAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["TaxWrite"]);

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Taxes.CountAsync());
        var payload = new
        {
            name = "IntegrationTax",
            rate = 17,
            isPercentage = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/tax", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("name").GetString().Should().Be("IntegrationTax");

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Taxes.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateTax_MissingClaim_ReturnsForbiddenAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Taxes.CountAsync());
        var payload = new
        {
            name = "BlockedTax",
            rate = 11,
            isPercentage = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/tax", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Taxes.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
