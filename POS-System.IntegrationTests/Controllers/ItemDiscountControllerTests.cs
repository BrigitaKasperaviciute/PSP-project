using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ItemDiscountControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ItemDiscountControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProductOnItemDiscounts.RemoveRange(db.ProductOnItemDiscounts.ToList());
        db.ServiceOnItemDiscounts.RemoveRange(db.ServiceOnItemDiscounts.ToList());
        db.ItemDiscounts.RemoveRange(db.ItemDiscounts.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllItemDiscounts_WithValidAuth_ReturnsOkWithDiscountList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // EndDate is null so the discount is not expired
        db.ItemDiscounts.Add(new ItemDiscount { Id = 1001, ItemDiscountId = 1001, Value = 10, IsPercentage = true, Description = "10% off", StartDate = null, EndDate = null, Version = DateTime.UtcNow, IsDeleted = false });
        db.ItemDiscounts.Add(new ItemDiscount { Id = 1002, ItemDiscountId = 1002, Value = 500, IsPercentage = false, Description = "5 off", StartDate = null, EndDate = null, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemDiscountRead");

        // Act
        var response = await client.GetAsync("/api/item-discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ItemDiscountResponse>>(_jsonOptions);
        body!.Results.Should().Contain(d => d.Description == "10% off");
    }

    [Fact]
    public async Task GetAllItemDiscounts_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/item-discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemDiscountById_WithExistingId_ReturnsOkWithDiscount()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ItemDiscounts.Add(new ItemDiscount { Id = 1003, ItemDiscountId = 1003, Value = 20, IsPercentage = true, Description = "Summer sale", StartDate = null, EndDate = null, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemDiscountRead");

        // Act
        var response = await client.GetAsync("/api/item-discount/1003");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>(_jsonOptions);
        body!.Description.Should().Be("Summer sale");
        body.Value.Should().Be(20);
        body.IsPercentage.Should().BeTrue();
    }

    [Fact]
    public async Task GetItemDiscountById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemDiscountRead");

        // Act
        var response = await client.GetAsync("/api/item-discount/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateItemDiscount_WithValidRequest_ReturnsOkWithCreatedDiscount()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemDiscountWrite");
        var request = new ItemDiscountRequest
        {
            Value = 15,
            IsPercentage = true,
            Description = "Black Friday",
            StartDate = null,
            EndDate = null
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>(_jsonOptions);
        body!.Description.Should().Be("Black Friday");
        body.Value.Should().Be(15);
        body.IsPercentage.Should().BeTrue();
        body.Id.Should().BeGreaterThan(0);

        // Verify database state
        using var assertScope = _factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.ItemDiscounts.FindAsync(body.Id);
        saved.Should().NotBeNull();
        saved!.Description.Should().Be("Black Friday");
        saved.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateItemDiscount_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new ItemDiscountRequest { Value = 5, IsPercentage = true, Description = "X", StartDate = null, EndDate = null };

        // Act
        var response = await client.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateItemDiscount_WithExistingId_ReturnsOkWithUpdatedDiscount()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ItemDiscounts.Add(new ItemDiscount { Id = 1004, ItemDiscountId = 1004, Value = 5, IsPercentage = true, Description = "Old discount", StartDate = null, EndDate = null, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemDiscountWrite");
        var request = new ItemDiscountRequest { Value = 25, IsPercentage = false, Description = "Updated discount", StartDate = null, EndDate = null };

        // Act
        var response = await client.PutAsJsonAsync("/api/item-discount/1004", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>(_jsonOptions);
        body!.Description.Should().Be("Updated discount");
        body.Value.Should().Be(25);

        // Verify old record is now marked deleted
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var old = await assertDb.ItemDiscounts.FindAsync(1004);
        old!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateItemDiscount_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemDiscountWrite");
        var request = new ItemDiscountRequest { Value = 1, IsPercentage = true, Description = "X", StartDate = null, EndDate = null };

        // Act
        var response = await client.PutAsJsonAsync("/api/item-discount/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteItemDiscount_WithExistingId_ReturnsOkAndSoftDeletesDiscount()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ItemDiscounts.Add(new ItemDiscount { Id = 1005, ItemDiscountId = 1005, Value = 8, IsPercentage = true, Description = "To delete", StartDate = null, EndDate = null, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemDiscountWrite");

        // Act
        var response = await client.DeleteAsync("/api/item-discount/1005");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify soft delete in database
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await assertDb.ItemDiscounts.FindAsync(1005);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteItemDiscount_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemDiscountWrite");

        // Act
        var response = await client.DeleteAsync("/api/item-discount/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── LINK / GET LINKED ────────────────────────────────────────────────────

    [Fact]
    public async Task LinkItemDiscountToProducts_WithValidIds_ReturnsOk()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ItemDiscounts.Add(new ItemDiscount { Id = 1006, ItemDiscountId = 1006, Value = 12, IsPercentage = true, Description = "Link test", StartDate = null, EndDate = null, Version = DateTime.UtcNow, IsDeleted = false });
        db.Products.Add(new Product { Id = 1006, ProductId = 1006, Name = "P", Description = "d", Price = 100, Stock = 1, ImageURL = "", Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemDiscountWrite");

        // Act
        var response = await client.PutAsJsonAsync("/api/item-discount/1006/link?itemsAreProducts=true", new[] { 1006 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetItemDiscountsLinkedToItem_WithProductId_ReturnsOkWithList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.Add(new Product { Id = 1007, ProductId = 1007, Name = "P2", Description = "d", Price = 200, Stock = 2, ImageURL = "", Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemDiscountRead");

        // Act
        var response = await client.GetAsync("/api/item-discount/item/1007?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ItemDiscountResponse>>(_jsonOptions);
        body.Should().BeEmpty();
    }
}
