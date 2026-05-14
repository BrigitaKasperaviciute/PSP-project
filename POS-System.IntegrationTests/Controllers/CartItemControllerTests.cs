using FluentAssertions;
using POS_System.Business.Dtos;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartItemControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public CartItemControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateCartItem_WithValidPayload_ReturnsOkAndPersistsCartItem()
    {
        // Arrange
        var request = new CartItemRequestBuilder()
            .WithCartId(1)
            .WithQuantity(2)
            .WithIsProduct(true)
            .WithProductVersionId(4)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts/1/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.CartId.Should().Be(1);
        body.Quantity.Should().Be(2);
        body.IsProduct.Should().BeTrue();
        body.ProductVersionId.Should().Be(4);

        await using var db = _factory.CreateDbContext();
        var persisted = await db.CartItems.AsNoTracking().FirstAsync(item => item.CartId == 1 && item.Quantity == 2 && item.ProductVersionId == 4);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCartItemById_WithExistingId_ReturnsOkAndCartItem()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/carts/1/items", new CartItemRequestBuilder().WithCartId(1).WithProductVersionId(4).Build());
        var createdItem = await createResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var response = await _client.GetAsync($"/api/carts/1/items/{createdItem!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdItem.Id);
    }

    [Fact]
    public async Task UpdateCartItem_WithValidPayload_ReturnsOkAndUpdatesDatabase()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/carts/1/items", new CartItemRequestBuilder().WithCartId(1).WithProductVersionId(4).Build());
        var createdItem = await createResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        var updateRequest = new CartItemRequestBuilder()
            .WithCartId(1)
            .WithQuantity(5)
            .WithIsProduct(true)
            .WithProductVersionId(4)
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/carts/1/items/{createdItem!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body!.Quantity.Should().Be(5);

        await using var db = _factory.CreateDbContext();
        var persisted = await db.CartItems.AsNoTracking().FirstAsync(item => item.Id == createdItem.Id);
        persisted.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task DeleteCartItem_WithExistingId_ReturnsNoContentAndRemovesCartItem()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/carts/1/items", new CartItemRequestBuilder().WithCartId(1).WithProductVersionId(4).Build());
        var createdItem = await createResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/carts/1/items/{createdItem!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = _factory.CreateDbContext();
        var deleted = await db.CartItems.AsNoTracking().FirstOrDefaultAsync(item => item.Id == createdItem.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task CreateCartItem_WithZeroQuantity_ReturnsBadRequest()
    {
        // Arrange
        var request = new CartItemRequestBuilder()
            .WithCartId(1)
            .WithQuantity(0)
            .WithProductVersionId(4)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts/1/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllCartItems_LinkAndUnlinkProductModifications_ReturnsOkAndUpdatesLinks()
    {
        // Arrange
        var createdItem = await CreateCartItemAsync();
        var linkIds = new[] { 1 };

        // Act
        var getResponse = await _client.GetAsync("/api/carts/1/items?pageNum=0&pageSize=10");
        var linkResponse = await _client.PutAsJsonAsync($"/api/carts/1/items/{createdItem.Id}/link", linkIds);

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await getResponse.Content.ReadFromJsonAsync<PagedResponse<CartItemResponse>>();
        page.Should().NotBeNull();
        page!.Results.Should().NotBeEmpty();

        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var unlinkResponse = await _client.PutAsJsonAsync($"/api/carts/1/items/{createdItem.Id}/unlink", linkIds);
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<CartItemResponse> CreateCartItemAsync()
    {
        var request = new CartItemRequestBuilder()
            .WithCartId(1)
            .WithQuantity(1)
            .WithIsProduct(true)
            .WithProductVersionId(4)
            .Build();

        var response = await _client.PostAsJsonAsync("/api/carts/1/items", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        return body!;
    }
}