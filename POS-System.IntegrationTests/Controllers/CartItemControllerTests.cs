using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class CartItemControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CartItemControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ServiceReservations.RemoveRange(db.ServiceReservations.ToList());
        db.ProductModificationOnCartItems.RemoveRange(db.ProductModificationOnCartItems.ToList());
        db.CartItems.RemoveRange(db.CartItems.ToList());
        db.Carts.RemoveRange(db.Carts.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static Cart MakeCart(int id) => new()
    {
        Id = id,
        EmployeeVersionId = 1,
        DateCreated = DateTime.UtcNow,
        IsDeleted = false,
        Status = CartStatusEnum.PENDING
    };

    private static CartItem MakeCartItem(int id, int cartId) => new()
    {
        Id = id,
        CartId = cartId,
        Quantity = 2,
        IsProduct = true,
        IsDeleted = false
    };

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCartItems_WithValidAuth_ReturnsOkWithList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(MakeCart(3001));
        db.CartItems.Add(MakeCartItem(3101, 3001));
        db.CartItems.Add(MakeCartItem(3102, 3001));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("CartItemRead");

        // Act
        var response = await client.GetAsync("/api/carts/3001/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartItemResponse>>(_jsonOptions);
        body!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAllCartItems_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/carts/3001/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCartItemById_WithExistingId_ReturnsOkWithCartItem()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(MakeCart(3002));
        db.CartItems.Add(MakeCartItem(3103, 3002));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("CartItemRead");

        // Act
        var response = await client.GetAsync("/api/carts/3002/items/3103");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>(_jsonOptions);
        body!.Id.Should().Be(3103);
        body.CartId.Should().Be(3002);
        body.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task GetCartItemById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("CartItemRead");

        // Act
        var response = await client.GetAsync("/api/carts/3002/items/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCartItem_WithExistingCart_ReturnsOkWithCreatedItem()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(MakeCart(3003));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("CartItemWrite");
        var request = new CartItemRequest
        {
            CartId = 3003,
            Quantity = 3,
            IsProduct = true,
            ProductVersionId = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/carts/3003/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>(_jsonOptions);
        body!.Id.Should().BeGreaterThan(0);
        body.CartId.Should().Be(3003);
        body.Quantity.Should().Be(3);
    }

    [Fact]
    public async Task CreateCartItem_WithNonExistentCart_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("CartItemWrite");
        var request = new CartItemRequest
        {
            CartId = 99999,
            Quantity = 1,
            IsProduct = true,
            ProductVersionId = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/carts/99999/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCartItem_WithExistingId_ReturnsOkWithUpdatedItem()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(MakeCart(3004));
        db.CartItems.Add(MakeCartItem(3104, 3004));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("CartItemWrite");
        var request = new CartItemRequest { CartId = 3004, Quantity = 5, IsProduct = false, ServiceVersionId = 1 };

        // Act
        var response = await client.PutAsJsonAsync("/api/carts/3004/items/3104", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartItemResponse>(_jsonOptions);
        body!.Quantity.Should().Be(5);
        body.IsProduct.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateCartItem_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("CartItemWrite");
        var request = new CartItemRequest { CartId = 3004, Quantity = 1, IsProduct = true, ProductVersionId = 1 };

        // Act
        var response = await client.PutAsJsonAsync("/api/carts/3004/items/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCartItem_WithExistingId_ReturnsNoContent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(MakeCart(3005));
        db.CartItems.Add(MakeCartItem(3105, 3005));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("CartItemWrite");

        // Act
        var response = await client.DeleteAsync("/api/carts/3005/items/3105");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await assertDb.CartItems.FindAsync(3105);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCartItem_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("CartItemWrite");

        // Act
        var response = await client.DeleteAsync("/api/carts/3005/items/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── LINK / UNLINK ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LinkCartItemToProductModifications_WithValidAuth_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("CartItemWrite");

        // Act — ManyToManyService is a silent no-op when entities don't exist
        var response = await client.PutAsJsonAsync("/api/carts/1/items/999/link", Array.Empty<int>());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlinkCartItemFromProductModifications_WithValidAuth_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("CartItemWrite");

        // Act — ManyToManyService is a silent no-op when entities don't exist
        var response = await client.PutAsJsonAsync("/api/carts/1/items/999/unlink", Array.Empty<int>());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
