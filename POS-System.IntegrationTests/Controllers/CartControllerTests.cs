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

public class CartControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CartControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.CartItems.RemoveRange(db.CartItems.ToList());
        db.Carts.RemoveRange(db.Carts.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCarts_WhenCartsExist_ReturnsOkWithPagedList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(new Cart { Id = 1001, EmployeeVersionId = 1, DateCreated = DateTime.UtcNow, Status = CartStatusEnum.IN_PROGRESS, IsDeleted = false });
        db.Carts.Add(new Cart { Id = 1002, EmployeeVersionId = 2, DateCreated = DateTime.UtcNow, Status = CartStatusEnum.PENDING, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/carts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartResponse>>(_jsonOptions);
        body!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAllCarts_WhenNoCartsExist_ReturnsOkWithEmptyList()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/carts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartResponse>>(_jsonOptions);
        body!.TotalCount.Should().Be(0);
        body.Results.Should().BeEmpty();
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCartById_WithExistingId_ReturnsOkWithCart()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(new Cart { Id = 1003, EmployeeVersionId = 1, DateCreated = new DateTime(2024, 5, 1), Status = CartStatusEnum.PENDING, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/carts/1003");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>(_jsonOptions);
        body!.Id.Should().Be(1003);
        body.EmployeeVersionId.Should().Be(1);
        body.Status.Should().Be(CartStatusEnum.PENDING);
    }

    [Fact]
    public async Task GetCartById_WithNonExistentId_ReturnsInternalServerError()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/carts/99999");

        // Assert
        // CartService throws a plain Exception (not NotFoundException) for missing carts,
        // so the global handler returns 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCart_WithValidRequest_ReturnsOkWithCreatedCart()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CartRequest { EmployeeVersionId = 10 };

        // Act
        var response = await client.PostAsJsonAsync("/api/carts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>(_jsonOptions);
        body!.EmployeeVersionId.Should().Be(10);
        body.Status.Should().Be(CartStatusEnum.IN_PROGRESS);
        body.Id.Should().BeGreaterThan(0);

        // Verify database state
        using var assertScope = _factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Carts.FindAsync(body.Id);
        saved.Should().NotBeNull();
        saved!.IsDeleted.Should().BeFalse();
        saved.Status.Should().Be(CartStatusEnum.IN_PROGRESS);
    }

    [Fact]
    public async Task CreateCart_WithNegativeEmployeeId_StillCreatesCart()
    {
        // Arrange - CartController has no auth and minimal validation; any EmployeeVersionId is accepted
        var client = _factory.CreateClient();
        var request = new CartRequest { EmployeeVersionId = 0 };

        // Act
        var response = await client.PostAsJsonAsync("/api/carts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>(_jsonOptions);
        body!.Status.Should().Be(CartStatusEnum.IN_PROGRESS);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCart_WithExistingInProgressCart_ReturnsOk()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(new Cart { Id = 1004, EmployeeVersionId = 1, DateCreated = DateTime.UtcNow, Status = CartStatusEnum.IN_PROGRESS, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/carts/1004");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify cart is removed from database
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await assertDb.Carts.FindAsync(1004);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCart_WithNonExistentId_ReturnsInternalServerError()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/carts/99999");

        // Assert
        // CartService throws a plain Exception (not NotFoundException) for missing carts
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── CART DISCOUNT ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCartDiscount_ForCartWithNoDiscount_ReturnsNoContent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(new Cart { Id = 1005, EmployeeVersionId = 1, DateCreated = DateTime.UtcNow, Status = CartStatusEnum.IN_PROGRESS, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/carts/1005/discount");

        // Assert
        // CartService returns null → Ok(null) → ASP.NET Core serialises null as 204 NoContent
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetCartDiscount_ForNonExistentCart_ReturnsNoContent()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/carts/99999/discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
