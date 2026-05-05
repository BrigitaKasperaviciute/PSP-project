using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartItemControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CartItemControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClientWithClaims("CartItemRead", "CartItemWrite", "ItemRead", "ItemWrite");
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.ServiceReservations.ExecuteDeleteAsync();
        await db.CartItems.ExecuteDeleteAsync();
        await db.ProductModifications.ExecuteDeleteAsync();
        await db.ProductOnTaxes.ExecuteDeleteAsync();
        await db.ProductOnItemDiscounts.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.Carts.ExecuteUpdateAsync(s => s.SetProperty(c => c.CartDiscountId, (string?)null));
        await db.Carts.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Creates a cart (no auth required) and returns its ID.</summary>
    private async Task<int> CreateCartAsync()
    {
        var cartClient = _factory.CreateClient();
        var cartResponse = await cartClient.PostAsJsonAsync("/api/carts", new CartBuilder().Build());
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();
        return cart!.Id;
    }

    /// <summary>Creates a product and returns its version ID.</summary>
    private async Task<int> CreateProductVersionIdAsync()
    {
        var productResponse = await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build());
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();
        return product!.Id;
    }

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithValidAuth_ReturnsOkAndCartItems()
    {
        // Arrange
        var cartId = await CreateCartAsync();
        var productVersionId = await CreateProductVersionIdAsync();
        await _client.PostAsJsonAsync($"/api/carts/{cartId}/items",
            new CartItemBuilder().WithCartId(cartId).WithProductVersionId(productVersionId).Build());

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}/items?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<CartItemResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/carts/1/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingCartItem_ReturnsCorrectItem()
    {
        // Arrange
        var cartId = await CreateCartAsync();
        var productVersionId = await CreateProductVersionIdAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/carts/{cartId}/items",
                new CartItemBuilder().WithCartId(cartId).WithQuantity(3).WithProductVersionId(productVersionId).Build()))
            .Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}/items/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.Quantity.Should().Be(3);
        body.CartId.Should().Be(cartId);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Arrange
        var cartId = await CreateCartAsync();

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}/items/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidProductRef_ReturnsOkAndPersistsCartItem()
    {
        // Arrange
        var cartId = await CreateCartAsync();
        var productVersionId = await CreateProductVersionIdAsync();
        var request = new CartItemBuilder()
            .WithCartId(cartId)
            .WithQuantity(2)
            .WithProductVersionId(productVersionId)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/carts/{cartId}/items", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body.Should().NotBeNull();
        body!.CartId.Should().Be(cartId);
        body.Quantity.Should().Be(2);
        body.IsProduct.Should().BeTrue();
        body.ProductVersionId.Should().Be(productVersionId);

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.CartItems.AsNoTracking().SingleOrDefaultAsync(ci => ci.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        // Arrange
        var readOnlyClient = _factory.CreateClientWithClaims("CartItemRead");

        // Act
        var response = await readOnlyClient.PostAsJsonAsync("/api/carts/1/items",
            new CartItemBuilder().WithCartId(1).WithProductVersionId(1).Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_ReturnsOkAndUpdatesQuantity()
    {
        // Arrange
        var cartId = await CreateCartAsync();
        var productVersionId = await CreateProductVersionIdAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/carts/{cartId}/items",
                new CartItemBuilder().WithCartId(cartId).WithQuantity(1).WithProductVersionId(productVersionId).Build()))
            .Content.ReadFromJsonAsync<CartItemResponse>();

        var updateRequest = new CartItemBuilder()
            .WithCartId(cartId)
            .WithQuantity(5)
            .WithProductVersionId(productVersionId)
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/carts/{cartId}/items/{created!.Id}", updateRequest);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>();
        body!.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        // Arrange
        var cartId = await CreateCartAsync();
        var request = new CartItemBuilder().WithCartId(cartId).WithProductVersionId(1).Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/carts/{cartId}/items/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsNoContentAndRemovesItem()
    {
        // Arrange
        var cartId = await CreateCartAsync();
        var productVersionId = await CreateProductVersionIdAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/carts/{cartId}/items",
                new CartItemBuilder().WithCartId(cartId).WithProductVersionId(productVersionId).Build()))
            .Content.ReadFromJsonAsync<CartItemResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{cartId}/items/{created!.Id}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var inDb = await db.CartItems.AsNoTracking().SingleOrDefaultAsync(ci => ci.Id == created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Arrange
        var cartId = await CreateCartAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{cartId}/items/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Link / Unlink ProductModifications ---

    [Fact]
    public async Task Link_CartItemToProductModification_ReturnsOkAndCreatesLink()
    {
        // Arrange
        var cartId = await CreateCartAsync();
        var productVersionId = await CreateProductVersionIdAsync();
        var cartItem = await (await _client.PostAsJsonAsync($"/api/carts/{cartId}/items",
                new CartItemBuilder().WithCartId(cartId).WithProductVersionId(productVersionId).Build()))
            .Content.ReadFromJsonAsync<CartItemResponse>();

        // Get a seeded product modification (ID 2 is seeded and active for product version 1)
        // Since we cleared products, there are no product modifications to link.
        // Create via DB directly.
        await using var db = _factory.CreateDbContext();
        var modification = new POS_System.Domain.Entities.ProductModification
        {
            ProductVersionId = productVersionId,
            ProductModificationId = 1,
            Name = "Extra",
            Description = "Extra option",
            Price = 100,
            Version = DateTime.UtcNow,
            IsDeleted = false
        };
        db.ProductModifications.Add(modification);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/carts/{cartId}/items/{cartItem!.Id}/link",
            new[] { modification.Id });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // ProductModificationOnCartItem inherits BaseManyToManyEntity<ProductModification, CartItem>: LeftEntityId=ModificationId, RightEntityId=CartItemId
        await using var db2 = _factory.CreateDbContext();
        var linkExists = await db2.ProductModificationOnCartItems.AsNoTracking()
            .AnyAsync(pm => pm.LeftEntityId == modification.Id && pm.RightEntityId == cartItem.Id);
        linkExists.Should().BeTrue();
    }
}

file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
