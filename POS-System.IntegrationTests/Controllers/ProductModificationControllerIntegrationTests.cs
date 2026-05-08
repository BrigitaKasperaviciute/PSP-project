using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class ProductModificationControllerIntegrationTests
{
    [Fact]
    public async Task CreateProductModification_ValidClaimAndPayload_PersistsModificationAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["ItemWrite"]);

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.ProductModifications.CountAsync());
        var payload = new
        {
            productVersionId = 4,
            name = "Integration add-on",
            description = "Extra topping",
            price = 50
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/product-modification", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("name").GetString().Should().Be("Integration add-on");

        var countAfter = await factory.ExecuteDbContextAsync(db => db.ProductModifications.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateProductModification_MissingClaim_ReturnsForbiddenAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.ProductModifications.CountAsync());
        var payload = new
        {
            productVersionId = 4,
            name = "Blocked add-on",
            description = "Blocked",
            price = 25
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/product-modification", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.ProductModifications.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
