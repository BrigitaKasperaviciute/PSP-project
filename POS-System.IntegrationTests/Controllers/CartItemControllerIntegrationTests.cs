using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class CartItemControllerIntegrationTests
{
    [Fact]
    public async Task CreateCartItem_ValidClaimAndPayload_PersistsItemAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["CartItemWrite"]);

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.CartItems.CountAsync());
        var payload = new
        {
            cartId = 3,
            quantity = 2,
            isProduct = true,
            productVersionId = 4,
            serviceVersionId = (int?)null
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/carts/3/items", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("cartId").GetInt32().Should().Be(3);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.CartItems.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateCartItem_MissingClaim_ReturnsForbiddenAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.CartItems.CountAsync());
        var payload = new
        {
            cartId = 3,
            quantity = 1,
            isProduct = true,
            productVersionId = 4,
            serviceVersionId = (int?)null
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/carts/3/items", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.CartItems.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
