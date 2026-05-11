using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartItemControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;
    private int _testCartId = 0;
    private int _testProductVersionId = 0;

    public CartItemControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        await _db.CartItems.ExecuteDeleteAsync();
        await _db.Carts.ExecuteDeleteAsync();
        await _db.ProductModifications.ExecuteDeleteAsync();
        await _db.Products.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();

        // Create test data
        var cartRequest = new CartRequestBuilder().Build();
        var cartResponse = await _client.PostAsJsonAsync("/api/carts", cartRequest);
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();
        _testCartId = cart!.Id;

        var productRequest = new ProductRequestBuilder().Build();
        var productResponse = await _client.PostAsJsonAsync("/api/product", productRequest);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();
        
        // Get product version (first version created with product)
        var versionsResponse = await _client.GetAsync($"/api/product/{product!.Id}/versions/");
        var versions = await versionsResponse.Content.ReadFromJsonAsync<List<ProductVersionResponse>>();
        _testProductVersionId = versions![0].Id;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateCartItem_WithValidPayload_ReturnsOkAndPersistsCartItem()
    {
        // Arrange
        var request = new CartItemRequestBuilder()
            .WithCartId(_testCartId)
            .WithQuantity(2)
            .WithIsProduct(true)
            .WithProductVersionId(_testProductVersionId)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/carts/{_testCartId}/items", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.Quantity.Should().Be(request.Quantity);

        // Assert - database state
        var persisted = await _db.CartItems.AsNoTracking()
            .FirstOrDefaultAsync(ci => ci.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Quantity.Should().Be(request.Quantity);
    }

    [Fact]
    public async Task GetAllCartItems_WithValidPageNumbers_ReturnsOkWithCartItems()
    {
        // Arrange
        var request1 = new CartItemRequestBuilder()
            .WithCartId(_testCartId)
            .WithQuantity(1)
            .WithProductVersionId(_testProductVersionId)
            .Build();
        
        await _client.PostAsJsonAsync($"/api/carts/{_testCartId}/items", request1);

        // Act
        var response = await _client.GetAsync($"/api/carts/{_testCartId}/items?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<CartItemResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCartItemById_WithExistingId_ReturnsOkWithCartItem()
    {
        // Arrange
        var createRequest = new CartItemRequestBuilder()
            .WithCartId(_testCartId)
            .WithProductVersionId(_testProductVersionId)
            .Build();
        
        var createResponse = await _client.PostAsJsonAsync($"/api/carts/{_testCartId}/items", createRequest);
        var createdItem = await createResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var response = await _client.GetAsync($"/api/carts/{_testCartId}/items/{createdItem!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body!.Id.Should().Be(createdItem.Id);
    }

    [Fact]
    public async Task UpdateCartItem_WithValidPayload_ReturnsOkAndUpdatesCartItem()
    {
        // Arrange
        var createRequest = new CartItemRequestBuilder()
            .WithCartId(_testCartId)
            .WithQuantity(1)
            .WithProductVersionId(_testProductVersionId)
            .Build();
        
        var createResponse = await _client.PostAsJsonAsync($"/api/carts/{_testCartId}/items", createRequest);
        var createdItem = await createResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var updateRequest = new CartItemRequestBuilder()
            .WithCartId(_testCartId)
            .WithQuantity(5)
            .WithProductVersionId(_testProductVersionId)
            .Build();
        
        var response = await _client.PutAsJsonAsync($"/api/carts/{_testCartId}/items/{createdItem!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body!.Quantity.Should().Be(updateRequest.Quantity);
    }

    [Fact]
    public async Task DeleteCartItem_WithExistingId_ReturnsNoContentAndDeletesCartItem()
    {
        // Arrange
        var createRequest = new CartItemRequestBuilder()
            .WithCartId(_testCartId)
            .WithProductVersionId(_testProductVersionId)
            .Build();
        
        var createResponse = await _client.PostAsJsonAsync($"/api/carts/{_testCartId}/items", createRequest);
        var createdItem = await createResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{_testCartId}/items/{createdItem!.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var deleted = await _db.CartItems.AsNoTracking()
            .FirstOrDefaultAsync(ci => ci.Id == createdItem.Id);
        deleted.Should().BeNull();
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetCartItemById_WithNonExistentCartId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/carts/999999/items/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateCartItem_WithNegativeQuantity_ReturnsBadRequest()
    {
        // Arrange
        var request = new CartItemRequestBuilder()
            .WithCartId(_testCartId)
            .WithQuantity(-1)
            .WithProductVersionId(_testProductVersionId)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/carts/{_testCartId}/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCartItem_WithZeroQuantity_ReturnsBadRequest()
    {
        // Arrange
        var request = new CartItemRequestBuilder()
            .WithCartId(_testCartId)
            .WithQuantity(0)
            .WithProductVersionId(_testProductVersionId)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/carts/{_testCartId}/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCartItem_WithNonExistentCartId_ReturnsNotFound()
    {
        // Arrange
        var request = new CartItemRequestBuilder()
            .WithCartId(999999)
            .WithProductVersionId(_testProductVersionId)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts/999999/items", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateCartItem_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new CartItemRequestBuilder()
            .WithCartId(_testCartId)
            .WithProductVersionId(_testProductVersionId)
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/carts/{_testCartId}/items/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCartItem_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync($"/api/carts/{_testCartId}/items/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

public class CartItemResponse
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int Quantity { get; set; }
    public bool IsProduct { get; set; }
    public int? ProductVersionId { get; set; }
    public int? ServiceVersionId { get; set; }
}
