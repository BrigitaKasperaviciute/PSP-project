using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartItemControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _cartClient;

    public CartItemControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClientWithClaims("CartItemRead", "CartItemWrite");
        _cartClient = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ServiceReservations.ExecuteDeleteAsync();
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.CartItems.ExecuteDeleteAsync();
        await db.Carts.ExecuteDeleteAsync();
        await db.ProductOnTaxes.ExecuteDeleteAsync();
        await db.ProductOnItemDiscounts.ExecuteDeleteAsync();
        await db.ProductModifications.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(int cartId, ProductResponse product)> SetupCartWithProductAsync()
    {
        var cart = await (await _cartClient.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDto>();
        var productClient = _factory.CreateClientWithClaims("ItemRead", "ItemWrite");
        var product = await (await productClient.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();
        return (cart!.Id, product!);
    }

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithCartItemReadClaim_ReturnsOk()
    {
        // Arrange
        var (cartId, product) = await SetupCartWithProductAsync();
        await _client.PostAsJsonAsync($"/api/carts/{cartId}/items",
            new CartItemRequest { CartId = cartId, Quantity = 1, IsProduct = true, ProductVersionId = product.Id });

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedCartItemResponse<CartItemResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/carts/1/items");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCartItem()
    {
        // Arrange
        var (cartId, product) = await SetupCartWithProductAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/carts/{cartId}/items",
                new CartItemRequest { CartId = cartId, Quantity = 2, IsProduct = true, ProductVersionId = product.Id }))
            .Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}/items/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body!.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var cart = await (await _cartClient.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDto>();
        var response = await _client.GetAsync($"/api/carts/{cart!.Id}/items/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithProductId_ReturnsOkAndPersists()
    {
        // Arrange
        var (cartId, product) = await SetupCartWithProductAsync();
        var request = new CartItemRequest { CartId = cartId, Quantity = 3, IsProduct = true, ProductVersionId = product.Id };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/carts/{cartId}/items", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body!.Quantity.Should().Be(3);
        body.IsProduct.Should().BeTrue();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.CartItems.AsNoTracking().SingleOrDefaultAsync(ci => ci.Id == body.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        var cart = await (await _cartClient.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDto>();
        var readOnlyClient = _factory.CreateClientWithClaims("CartItemRead");
        var response = await readOnlyClient.PostAsJsonAsync($"/api/carts/{cart!.Id}/items",
            new CartItemRequest { CartId = cart!.Id, Quantity = 1, IsProduct = true });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidQuantity_ReturnsUpdatedCartItem()
    {
        // Arrange
        var (cartId, product) = await SetupCartWithProductAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/carts/{cartId}/items",
                new CartItemRequest { CartId = cartId, Quantity = 1, IsProduct = true, ProductVersionId = product.Id }))
            .Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/carts/{cartId}/items/{created!.Id}",
            new CartItemRequest { CartId = cartId, Quantity = 5, IsProduct = true, ProductVersionId = product.Id });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body!.Quantity.Should().Be(5);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsNoContent()
    {
        // Arrange
        var (cartId, product) = await SetupCartWithProductAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/carts/{cartId}/items",
                new CartItemRequest { CartId = cartId, Quantity = 1, IsProduct = true, ProductVersionId = product.Id }))
            .Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{cartId}/items/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        var cart = await (await _cartClient.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDto>();
        var response = await _client.DeleteAsync($"/api/carts/{cart!.Id}/items/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record CartDto(int Id, int EmployeeVersionId);
file record PagedCartItemResponse<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
