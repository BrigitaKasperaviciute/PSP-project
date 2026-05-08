using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class ProductControllerIntegrationTests
{
    [Fact]
    public async Task CreateProduct_ValidClaimAndPayload_PersistsProductAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["ItemWrite"]);

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Products.CountAsync());
        var payload = new
        {
            name = "Integration Product",
            description = "Integration test product",
            price = 459,
            imageURL = "https://example.com/product.png",
            stock = 12
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/product", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("name").GetString().Should().Be("Integration Product");

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Products.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateProduct_MissingClaim_ReturnsForbiddenAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Products.CountAsync());
        var payload = new
        {
            name = "Forbidden Product",
            description = "Should be blocked",
            price = 123,
            imageURL = "https://example.com/product.png",
            stock = 3
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/product", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Products.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
