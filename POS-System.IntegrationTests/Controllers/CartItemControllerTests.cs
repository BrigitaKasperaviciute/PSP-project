using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

/// <summary>
/// Seeded state relevant to these tests:
///   Cart 1  (PENDING)   → CartItems 1 (product) and 2 (service)
///   Cart 3  (IN_PROGRESS) → no items
///   Products: Id=4 is active
/// </summary>
public class CartItemControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;
    private readonly HttpClient _writeClient;
    private readonly HttpClient _anonClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CartItemControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _readClient  = factory.CreateClientWithClaims("CartItemRead");
        _writeClient = factory.CreateClientWithClaims("CartItemRead", "CartItemWrite");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET /api/carts/{cartId}/items ────────────────────────────────────────

    [Fact]
    public async Task GetAll_ForExistingCart_ReturnsOkWithPagedResult()
    {
        // Arrange — cart 1 has 2 seeded cart items

        // Act
        var response = await _readClient.GetAsync("/api/carts/1/items");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<CartItemResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/carts/1/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/carts/{cartId}/items/{id} ───────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingCartAndItem_ReturnsOkWithCartItem()
    {
        // Arrange — cart item Id=1 belongs to cart Id=1

        // Act
        var response = await _readClient.GetAsync("/api/carts/1/items/1");
        var body = await response.Content.ReadAsStringAsync();
        var item = JsonSerializer.Deserialize<CartItemResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        item.Should().NotBeNull();
        item!.Id.Should().Be(1);
        item.CartId.Should().Be(1);
        item.IsProduct.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_WithNonExistentItem_ReturnsNotFound()
    {
        // Arrange — item 9999 does not exist in cart 1

        // Act
        var response = await _readClient.GetAsync("/api/carts/1/items/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/carts/{cartId}/items ───────────────────────────────────────

    [Fact]
    public async Task Create_WithValidProductItem_ReturnsOkAndPersistsCartItem()
    {
        // Arrange — add product (Id=4, active) to cart 3 (IN_PROGRESS, no items yet)
        var request = new CartItemRequest
        {
            CartId          = 3,
            Quantity        = 2,
            IsProduct       = true,
            ProductVersionId = 4
        };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/carts/3/items", request);
        var body = await response.Content.ReadAsStringAsync();
        var item = JsonSerializer.Deserialize<CartItemResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        item.Should().NotBeNull();
        item!.CartId.Should().Be(3);
        item.Quantity.Should().Be(2);
        item.IsProduct.Should().BeTrue();

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.CartItems.FindAsync(item.Id);
        persisted.Should().NotBeNull();
        persisted!.ProductVersionId.Should().Be(4);
    }

    [Fact]
    public async Task Create_WithNonExistentCart_ReturnsBadRequest()
    {
        // Arrange — cart 9999 does not exist
        var request = new CartItemRequest
        {
            CartId    = 9999,
            Quantity  = 1,
            IsProduct = true,
            ProductVersionId = 4
        };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/carts/9999/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT /api/carts/{cartId}/items/{id} ───────────────────────────────────

    [Fact]
    public async Task Update_WithExistingCartItem_ReturnsOkWithUpdatedItem()
    {
        // Arrange — update cart item 1 (in cart 1)
        var request = new CartItemRequest
        {
            CartId           = 1,
            Quantity         = 5,
            IsProduct        = true,
            ProductVersionId = 4
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/carts/1/items/1", request);
        var body = await response.Content.ReadAsStringAsync();
        var item = JsonSerializer.Deserialize<CartItemResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        item!.Quantity.Should().Be(5);

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.CartItems.FindAsync(1);
        persisted!.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Update_WithNonExistentItem_ReturnsNotFound()
    {
        // Arrange
        var request = new CartItemRequest
        {
            CartId           = 1,
            Quantity         = 1,
            IsProduct        = true,
            ProductVersionId = 4
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/carts/1/items/9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/carts/{cartId}/items/{id} ────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingCartItem_ReturnsNoContentAndRemovesItem()
    {
        // Arrange — delete item 1 from cart 1
        var beforeResponse = await _readClient.GetAsync("/api/carts/1/items/1");
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var response = await _writeClient.DeleteAsync("/api/carts/1/items/1");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – removed from database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.CartItems.FindAsync(1);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WithNonExistentItem_ReturnsNotFound()
    {
        // Act
        var response = await _writeClient.DeleteAsync("/api/carts/1/items/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/carts/{cartId}/items/{id}/link ───────────────────────────────

    [Fact]
    public async Task LinkToProductModifications_WithValidIds_ReturnsOk()
    {
        // Arrange — cart item 1 (product), product modification Id=2 (active)
        var modificationIds = new[] { 2 };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/carts/1/items/1/link", modificationIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – link persisted
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var link = db.ProductModificationOnCartItems
            .FirstOrDefault(pm => pm.LeftEntityId == 2 && pm.RightEntityId == 1);
        link.Should().NotBeNull();
    }

    [Fact]
    public async Task UnlinkFromProductModifications_WithValidIds_ReturnsOk()
    {
        // Arrange — link first, then unlink
        await _writeClient.PutAsJsonAsync("/api/carts/1/items/1/link", new[] { 2 });

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/carts/1/items/1/unlink", new[] { 2 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – link is soft-deleted (EndDate set)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var link = db.ProductModificationOnCartItems
            .FirstOrDefault(pm => pm.LeftEntityId == 2 && pm.RightEntityId == 1);
        link.Should().NotBeNull();
        link!.EndDate.Should().NotBeNull();
    }
}
