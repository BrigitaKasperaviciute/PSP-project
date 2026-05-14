using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class CartItemControllerIntegrationTests
{
    [Fact]
    public async Task GetAll_WithReadClaim_ReturnsPagedCartItems()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CartItemRead" });

        // Act
        var response = await client.GetAsync("/api/carts/1/items?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartItemResponse>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThan(0);
        body.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_WithReadClaim_ReturnsCartItem()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CartItemRead" });

        // Act
        var response = await client.GetAsync("/api/carts/1/items/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(1);
        body.CartId.Should().Be(1);
    }

    [Fact]
    public async Task Create_WithWriteClaim_PersistsCartItem()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CartItemWrite" });

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.CartItems.CountAsync());
        var payload = new CartItemRequest
        {
            CartId = 1,
            Quantity = 3,
            IsProduct = true,
            ProductVersionId = 4,
            ServiceVersionId = null
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/carts/1/items", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.CartId.Should().Be(1);
        body.Quantity.Should().Be(payload.Quantity);
        body.ProductVersionId.Should().Be(payload.ProductVersionId);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.CartItems.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task Update_WithWriteClaim_UpdatesCartItem()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CartItemWrite" });

        // Arrange
        var payload = new CartItemRequest
        {
            CartId = 1,
            Quantity = 8,
            IsProduct = true,
            ProductVersionId = 4,
            ServiceVersionId = null
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/carts/1/items/1", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.Quantity.Should().Be(8);

        var persisted = await factory.ExecuteDbContextAsync(db => db.CartItems.SingleAsync(item => item.Id == 1));
        persisted.Quantity.Should().Be(8);
    }

    [Fact]
    public async Task Delete_WithWriteClaim_RemovesCartItem()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CartItemWrite" });

        // Arrange - create a cart item so we don't delete a seeded one
        var created = await (await client.PostAsJsonAsync("/api/carts/1/items", new CartItemRequest
        {
            CartId = 1,
            Quantity = 2,
            IsProduct = true,
            ProductVersionId = 4,
            ServiceVersionId = null
        })).Content.ReadFromJsonAsync<CartItemResponse>();

        created.Should().NotBeNull();

        // Act
        var response = await client.DeleteAsync($"/api/carts/1/items/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var exists = await factory.ExecuteDbContextAsync(db => db.CartItems.AnyAsync(item => item.Id == created.Id));
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task LinkAndUnlink_WithWriteClaim_UpdatesJoinRows()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CartItemWrite" });

        // Arrange - create a fresh cart item and use seeded product modifications 2 and 4.
        var created = await (await client.PostAsJsonAsync("/api/carts/1/items", new CartItemRequest
        {
            CartId = 1,
            Quantity = 1,
            IsProduct = true,
            ProductVersionId = 4,
            ServiceVersionId = null
        })).Content.ReadFromJsonAsync<CartItemResponse>();

        created.Should().NotBeNull();

        var linkPayload = new[] { 2, 4 };

        // Act - link
        var linkResponse = await client.PutAsJsonAsync($"/api/carts/1/items/{created!.Id}/link", linkPayload);

        // Assert - link
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var linkedCount = await factory.ExecuteDbContextAsync(db => db.ProductModificationOnCartItems.CountAsync(x => x.RightEntityId == created.Id));
        linkedCount.Should().Be(2);

        // Act - unlink
        var unlinkResponse = await client.PutAsJsonAsync($"/api/carts/1/items/{created.Id}/unlink", linkPayload);

        // Assert - unlink
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var remainingCount = await factory.ExecuteDbContextAsync(db => db.ProductModificationOnCartItems.CountAsync(x => x.RightEntityId == created.Id && x.EndDate == null));
        remainingCount.Should().Be(0);
    }
}
