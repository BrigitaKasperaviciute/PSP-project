using System.Net;
using Microsoft.AspNetCore.Http;
using Xunit;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartItemControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    // Seeded: Cart Ids 1–4, CartItem Ids 1–4
    // Test-created carts will get Ids > 4, cart items > 4
    private int _testCartId;

    public CartItemControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        // Remove test-created cart items and carts (preserve seeded data)
        await db.CartItems.Where(ci => ci.CartId > 4).ExecuteDeleteAsync();
        await db.Carts.Where(c => c.Id > 4).ExecuteDeleteAsync();

        // Create a fresh cart for this test class
        var cartResponse = await _client.PostAsJsonAsync("/api/carts",
            new CartRequest { EmployeeVersionId = 1 });
        cartResponse.EnsureSuccessStatusCode();
        var cart = (await cartResponse.Content.ReadFromJsonAsync<CartResponse>())!;
        _testCartId = cart.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetAllCartItems ---------------

    [Fact]
    public async Task GetAllCartItems_WhenItemsExist_ReturnsOkWithPagedResults()
    {
        // Arrange – seeded CartItems in CartId=1
        const int cartId = 1;

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}/items?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartItemResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllCartItems_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync($"/api/carts/{_testCartId}/items?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- GetCartItemByIdAndCartId ---------------

    [Fact]
    public async Task GetCartItemByIdAndCartId_WhenItemExists_ReturnsOkWithItem()
    {
        // Arrange – seeded CartItem Id=1 belongs to CartId=1
        const int cartId = 1;
        const int itemId = 1;

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}/items/{itemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(itemId);
        body.CartId.Should().Be(cartId);
    }

    [Fact]
    public async Task GetCartItemByIdAndCartId_WhenItemDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentItemId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/carts/{_testCartId}/items/{nonExistentItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- CreateCartItem ---------------

    [Fact]
    public async Task CreateCartItem_WithValidProductRequest_ReturnsOkAndPersists()
    {
        // Arrange – seeded product version Id=1
        var request = new CartItemRequest
        {
            CartId = _testCartId,
            ProductVersionId = 1,
            ServiceVersionId = null,
            Quantity = 2,
            IsProduct = true
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/carts/{_testCartId}/items", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.CartId.Should().Be(_testCartId);
        body.Quantity.Should().Be(2);
        body.IsProduct.Should().BeTrue();

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.CartItems.AsNoTracking()
            .SingleOrDefaultAsync(ci => ci.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateCartItem_WhenCartDoesNotExist_ReturnsBadRequest()
    {
        // Arrange – nonexistent cart
        var request = new CartItemRequest
        {
            CartId = 99999,
            ProductVersionId = 1,
            ServiceVersionId = null,
            Quantity = 1,
            IsProduct = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts/99999/items", request);

        // Assert – CartItemService throws BadRequestException when cart not found → 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --------------- UpdateCartItem ---------------

    [Fact]
    public async Task UpdateCartItem_WithValidRequest_ReturnsOkAndUpdates()
    {
        // Arrange
        var created = await CreateCartItemAsync(_testCartId, productVersionId: 1, quantity: 1);
        var updateRequest = new CartItemRequest
        {
            CartId = _testCartId,
            ProductVersionId = 1,
            ServiceVersionId = null,
            Quantity = 5,
            IsProduct = true
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/carts/{_testCartId}/items/{created.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.Quantity.Should().Be(5);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.CartItems.AsNoTracking()
            .SingleOrDefaultAsync(ci => ci.Id == created.Id);
        persisted.Should().NotBeNull();
        persisted!.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task UpdateCartItem_WhenItemDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var request = new CartItemRequest
        {
            CartId = _testCartId,
            ProductVersionId = 1,
            ServiceVersionId = null,
            Quantity = 1,
            IsProduct = true
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/carts/{_testCartId}/items/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- DeleteCartItem ---------------

    [Fact]
    public async Task DeleteCartItem_WhenItemExists_ReturnsNoContentAndRemovesFromDb()
    {
        // Arrange
        var created = await CreateCartItemAsync(_testCartId, productVersionId: 1, quantity: 3);

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{_testCartId}/items/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – CartItemRepository.Delete is a hard delete (not soft delete)
        await using var db = _factory.CreateDbContext();
        var persisted = await db.CartItems.AsNoTracking()
            .SingleOrDefaultAsync(ci => ci.Id == created.Id);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCartItem_WhenItemDoesNotExist_ReturnsNotFound()
    {
        // Arrange

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{_testCartId}/items/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- LinkCartItemToProductModifications ---------------

    [Fact]
    public async Task LinkCartItemToProductModifications_WithValidIds_ReturnsOk()
    {
        // Arrange – create a cart item, link to seeded ProductModification Id=2 (active)
        var cartItem = await CreateCartItemAsync(_testCartId, productVersionId: 1, quantity: 1);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/carts/{_testCartId}/items/{cartItem.Id}/link",
            new[] { 2 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkCartItemToProductModifications_WhenItemDoesNotExist_ReturnsOk()
    {
        // Arrange – non-existent cart item

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/carts/{_testCartId}/items/99999/link",
            new[] { 2 });

        // Assert – ManyToManyService silently does nothing when CartItem not found, returns 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --------------- UnlinkCartItemFromProductModifications ---------------

    [Fact]
    public async Task UnlinkCartItemFromProductModifications_AfterLinking_ReturnsOk()
    {
        // Arrange – create a cart item, link to ProductModification Id=2, then unlink
        var cartItem = await CreateCartItemAsync(_testCartId, productVersionId: 1, quantity: 1);
        await _client.PutAsJsonAsync(
            $"/api/carts/{_testCartId}/items/{cartItem.Id}/link",
            new[] { 2 });

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/carts/{_testCartId}/items/{cartItem.Id}/unlink",
            new[] { 2 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- helpers ----

    private async Task<CartItemResponse> CreateCartItemAsync(
        int cartId, int productVersionId, int quantity)
    {
        var response = await _client.PostAsJsonAsync($"/api/carts/{cartId}/items",
            new CartItemRequest
            {
                CartId = cartId,
                ProductVersionId = productVersionId,
                ServiceVersionId = null,
                Quantity = quantity,
                IsProduct = true
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CartItemResponse>())!;
    }
}
