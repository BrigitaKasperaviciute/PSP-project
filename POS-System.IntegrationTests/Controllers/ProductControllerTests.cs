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

public class ProductControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ProductControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProductModificationOnCartItems.RemoveRange(db.ProductModificationOnCartItems.ToList());
        db.CartItems.RemoveRange(db.CartItems.ToList());
        db.ProductOnTaxes.RemoveRange(db.ProductOnTaxes.ToList());
        db.ProductOnItemDiscounts.RemoveRange(db.ProductOnItemDiscounts.ToList());
        db.Products.RemoveRange(db.Products.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllProducts_WithValidAuth_ReturnsOkWithActiveProducts()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.Add(new Product { Id = 1001, ProductId = 1001, Name = "Widget", Description = "A widget", Price = 999, Stock = 10, ImageURL = "http://x.com/w.jpg", Version = DateTime.UtcNow, IsDeleted = false });
        db.Products.Add(new Product { Id = 1002, ProductId = 1001, Name = "Widget v2", Description = "A widget v2", Price = 1099, Stock = 8, ImageURL = "http://x.com/w2.jpg", Version = DateTime.UtcNow, IsDeleted = true });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product?onlyActive=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>(_jsonOptions);
        body!.Results.Should().Contain(p => p.Name == "Widget");
        body.Results.Should().NotContain(p => p.Name == "Widget v2");
    }

    [Fact]
    public async Task GetAllProducts_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/product");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProductById_WithExistingId_ReturnsOkWithProduct()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.Add(new Product { Id = 1003, ProductId = 1003, Name = "Gadget", Description = "Cool gadget", Price = 2499, Stock = 5, ImageURL = "", Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product/1003");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>(_jsonOptions);
        body!.Name.Should().Be("Gadget");
        body.Price.Should().Be(2499);
        body.Stock.Should().Be(5);
    }

    [Fact]
    public async Task GetProductById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProduct_WithValidRequest_ReturnsOkWithCreatedProduct()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemWrite");
        var request = new ProductRequest
        {
            Name = "NewProduct",
            Description = "Brand new product",
            Price = 1299,
            ImageURL = "http://images.com/new.jpg",
            Stock = 20
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/product", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>(_jsonOptions);
        body!.Name.Should().Be("NewProduct");
        body.Price.Should().Be(1299);
        body.Stock.Should().Be(20);
        body.Id.Should().BeGreaterThan(0);

        // Verify database state
        using var assertScope = _factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Products.FindAsync(body.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("NewProduct");
        saved.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProduct_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new ProductRequest { Name = "X", Description = "X", Price = 1, ImageURL = "", Stock = 1 };

        // Act
        var response = await client.PostAsJsonAsync("/api/product", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProduct_WithExistingId_ReturnsOkWithUpdatedProduct()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.Add(new Product { Id = 1004, ProductId = 1004, Name = "OldProduct", Description = "Old", Price = 500, Stock = 3, ImageURL = "", Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemWrite");
        var request = new ProductRequest { Name = "UpdatedProduct", Description = "Updated", Price = 750, ImageURL = "https://test.com/img.jpg", Stock = 10 };

        // Act
        var response = await client.PutAsJsonAsync("/api/product/1004", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>(_jsonOptions);
        body!.Name.Should().Be("UpdatedProduct");
        body.Price.Should().Be(750);

        // Verify old record is marked deleted
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var old = await assertDb.Products.FindAsync(1004);
        old!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProduct_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemWrite");
        var request = new ProductRequest { Name = "X", Description = "X", Price = 1, ImageURL = "https://test.com/img.jpg", Stock = 1 };

        // Act
        var response = await client.PutAsJsonAsync("/api/product/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteProduct_WithExistingId_ReturnsOkAndSoftDeletesProduct()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.Add(new Product { Id = 1005, ProductId = 1005, Name = "ToDelete", Description = "Delete me", Price = 100, Stock = 1, ImageURL = "", Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemWrite");

        // Act
        var response = await client.DeleteAsync("/api/product/1005");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify soft delete in database
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await assertDb.Products.FindAsync(1005);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteProduct_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemWrite");

        // Act
        var response = await client.DeleteAsync("/api/product/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET VERSIONS ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProductVersions_WithExistingProductId_ReturnsOkWithVersionList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.Add(new Product { Id = 1006, ProductId = 2001, Name = "V1", Description = "v1", Price = 100, Stock = 1, ImageURL = "", Version = new DateTime(2024, 1, 1), IsDeleted = true });
        db.Products.Add(new Product { Id = 1007, ProductId = 2001, Name = "V2", Description = "v2", Price = 150, Stock = 2, ImageURL = "", Version = new DateTime(2024, 6, 1), IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product/2001/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>(_jsonOptions);
        body!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetProductVersions_WithNonExistentProductId_ReturnsOkWithEmptyList()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product/99999/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>(_jsonOptions);
        body.Should().BeEmpty();
    }

    // ── GET LINKED TO TAX ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetProductsLinkedToTaxId_WithAnyId_ReturnsOkWithEmptyList()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product/tax/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>(_jsonOptions);
        body.Should().BeEmpty();
    }

    // ── GET LINKED TO ITEM DISCOUNT ───────────────────────────────────────────

    [Fact]
    public async Task GetProductsLinkedToItemDiscountId_WithExistingDiscountAndNoProducts_ReturnsOkWithEmptyList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ItemDiscounts.Add(new ItemDiscount
        {
            Id = 5001,
            ItemDiscountId = 5001,
            Value = 10,
            IsPercentage = true,
            Description = "Test Discount",
            StartDate = null,
            EndDate = null,
            Version = DateTime.UtcNow,
            IsDeleted = false
        });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product/item-discount/5001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>(_jsonOptions);
        body.Should().BeEmpty();
    }
}
