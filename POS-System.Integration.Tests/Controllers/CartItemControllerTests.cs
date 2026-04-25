using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

public class CartItemControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public CartItemControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Seeded: Cart Id=1 contains CartItem Id=1 (ProductVersionId=1) and Id=2 (ServiceVersionId=1).
    // Cart Id=2 contains CartItems Id=3 and Id=4.

    [Fact]
    public async Task GetAll_ValidToken_ReturnsOkWithPagedResult()
    {
        // Arrange
        const int cartId = 1; // has seeded items

        // Act
        var response = await _authClient.GetAsync($"/api/carts/{cartId}/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartItemResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        const int cartId = 1;

        // Act
        var response = await _anonClient.GetAsync($"/api/carts/{cartId}/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingItemInCart_ReturnsOkWithCartItem()
    {
        // Arrange
        const int cartId = 1;
        const int itemId = 1; // seeded CartItem for cart 1

        // Act
        var response = await _authClient.GetAsync($"/api/carts/{cartId}/items/{itemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(itemId);
        body.CartId.Should().Be(cartId);
        body.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task GetById_NonExistentItem_ReturnsNotFound()
    {
        // Arrange
        const int cartId = 1;
        const int nonExistentItemId = 99999;

        // Act
        var response = await _authClient.GetAsync($"/api/carts/{cartId}/items/{nonExistentItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithCartItem()
    {
        // Arrange
        const int cartId = 1; // seeded cart
        var request = new CartItemRequest
        {
            CartId = cartId,
            Quantity = 3,
            IsProduct = true,
            ProductVersionId = 4 // seeded active product
        };

        // Act
        var response = await _authClient.PostAsJsonAsync($"/api/carts/{cartId}/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.CartId.Should().Be(cartId);
        body.Quantity.Should().Be(3);
        body.IsProduct.Should().BeTrue();

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.CartItems.FindAsync(body.Id);
        saved.Should().NotBeNull();
        saved!.Quantity.Should().Be(3);
    }

    [Fact]
    public async Task Create_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        const int cartId = 1;
        var request = new CartItemRequest { CartId = cartId, Quantity = 1, IsProduct = true, ProductVersionId = 4 };

        // Act
        var response = await _anonClient.PostAsJsonAsync($"/api/carts/{cartId}/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ExistingItem_ReturnsOkWithUpdatedCartItem()
    {
        // Arrange – create a dedicated cart item to update
        const int cartId = 1;
        var createReq = new CartItemRequest { CartId = cartId, Quantity = 1, IsProduct = true, ProductVersionId = 4 };
        var created = await (await _authClient.PostAsJsonAsync($"/api/carts/{cartId}/items", createReq))
            .Content.ReadFromJsonAsync<CartItemResponse>();

        var updateReq = new CartItemRequest { CartId = cartId, Quantity = 5, IsProduct = true, ProductVersionId = 4 };

        // Act
        var response = await _authClient.PutAsJsonAsync(
            $"/api/carts/{cartId}/items/{created!.Id}", updateReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Update_NonExistentItem_ReturnsNotFound()
    {
        // Arrange
        const int cartId = 1;
        var request = new CartItemRequest { CartId = cartId, Quantity = 1, IsProduct = true, ProductVersionId = 4 };

        // Act
        var response = await _authClient.PutAsJsonAsync($"/api/carts/{cartId}/items/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingItem_ReturnsNoContent()
    {
        // Arrange – create a dedicated item to delete
        const int cartId = 1;
        var createReq = new CartItemRequest { CartId = cartId, Quantity = 1, IsProduct = true, ProductVersionId = 4 };
        var created = await (await _authClient.PostAsJsonAsync($"/api/carts/{cartId}/items", createReq))
            .Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var response = await _authClient.DeleteAsync($"/api/carts/{cartId}/items/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_NonExistentItem_ReturnsNotFound()
    {
        // Arrange
        const int cartId = 1;

        // Act
        var response = await _authClient.DeleteAsync($"/api/carts/{cartId}/items/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnlinkFromProductModifications_ValidIds_ReturnsOk()
    {
        // Arrange – create a cart item and unlink modifications from it (even if none linked, should succeed)
        const int cartId = 1;
        var createReq = new CartItemRequest { CartId = cartId, Quantity = 1, IsProduct = true, ProductVersionId = 4 };
        var created = await (await _authClient.PostAsJsonAsync($"/api/carts/{cartId}/items", createReq))
            .Content.ReadFromJsonAsync<CartItemResponse>();
        var modificationIds = new[] { 2 };

        // Act
        var response = await _authClient.PutAsJsonAsync(
            $"/api/carts/{cartId}/items/{created!.Id}/unlink", modificationIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkToProductModifications_ValidIds_ReturnsOk()
    {
        // Arrange – create item, then link a product modification to it
        const int cartId = 1;
        var createReq = new CartItemRequest { CartId = cartId, Quantity = 1, IsProduct = true, ProductVersionId = 1 };
        var created = await (await _authClient.PostAsJsonAsync($"/api/carts/{cartId}/items", createReq))
            .Content.ReadFromJsonAsync<CartItemResponse>();
        var modificationIds = new[] { 2 }; // seeded ProductModification Id=2 for ProductVersionId=1

        // Act
        var response = await _authClient.PutAsJsonAsync(
            $"/api/carts/{cartId}/items/{created!.Id}/link", modificationIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkToProductModifications_NonExistentItem_ReturnsOk()
    {
        // Arrange — ManyToManyService.LinkItemToItemsAsync silently succeeds when the source item
        // is not found, so the controller returns 200.
        const int cartId = 1;
        var modificationIds = new[] { 2 };

        // Act
        var response = await _authClient.PutAsJsonAsync(
            $"/api/carts/{cartId}/items/99999/link", modificationIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
