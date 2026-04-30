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

public class ProductModificationControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ProductModificationControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProductModificationOnCartItems.RemoveRange(db.ProductModificationOnCartItems.ToList());
        db.ProductModifications.RemoveRange(db.ProductModifications.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static ProductModification MakeMod(int id, int modId, bool deleted = false) => new()
    {
        Id = id,
        ProductModificationId = modId,
        ProductVersionId = 1,
        Name = $"Mod-{id}",
        Description = "A modification",
        Price = 100,
        Version = DateTime.UtcNow,
        IsDeleted = deleted
    };

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllProductModifications_WithValidAuth_ReturnsOkWithList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProductModifications.Add(MakeMod(1001, 1001));
        db.ProductModifications.Add(MakeMod(1002, 1002));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product-modification");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductModificationResponse>>(_jsonOptions);
        body!.Results.Should().Contain(m => m.Name == "Mod-1001");
    }

    [Fact]
    public async Task GetAllProductModifications_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/product-modification");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProductModificationById_WithExistingId_ReturnsOkWithModification()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProductModifications.Add(MakeMod(1003, 1003));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product-modification/1003");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>(_jsonOptions);
        body!.Name.Should().Be("Mod-1003");
        body.Price.Should().Be(100);
    }

    [Fact]
    public async Task GetProductModificationById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product-modification/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET VERSIONS ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProductModificationVersions_WithExistingModId_ReturnsOkWithVersionList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProductModifications.Add(MakeMod(1004, 2001, deleted: true));
        db.ProductModifications.Add(MakeMod(1005, 2001, deleted: false));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product-modification/2001/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductModificationResponse>>(_jsonOptions);
        body!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetProductModificationVersions_WithNonExistentModId_ReturnsOkWithEmptyList()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product-modification/99999/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductModificationResponse>>(_jsonOptions);
        body.Should().BeEmpty();
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProductModification_WithValidRequest_ReturnsOkWithCreatedModification()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemWrite");
        var request = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name = "Extra Sauce",
            Description = "Additional sauce on the side",
            Price = 50
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/product-modification", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>(_jsonOptions);
        body!.Name.Should().Be("Extra Sauce");
        body.Price.Should().Be(50);
        body.Id.Should().BeGreaterThan(0);

        // Verify database state
        using var assertScope = _factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.ProductModifications.FindAsync(body.Id);
        saved.Should().NotBeNull();
        saved!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProductModification_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new ProductModificationRequest { ProductVersionId = 1, Name = "X", Description = "X", Price = 1 };

        // Act
        var response = await client.PostAsJsonAsync("/api/product-modification", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProductModification_WithExistingId_ReturnsOkWithUpdatedModification()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProductModifications.Add(MakeMod(1006, 1006));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemWrite");
        var request = new ProductModificationRequest { ProductVersionId = 1, Name = "Updated Mod", Description = "Updated desc", Price = 200 };

        // Act
        var response = await client.PutAsJsonAsync("/api/product-modification/1006", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>(_jsonOptions);
        body!.Name.Should().Be("Updated Mod");

        // Verify old record is marked deleted
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var old = await assertDb.ProductModifications.FindAsync(1006);
        old!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProductModification_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemWrite");
        var request = new ProductModificationRequest { ProductVersionId = 1, Name = "X", Description = "X", Price = 1 };

        // Act
        var response = await client.PutAsJsonAsync("/api/product-modification/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteProductModification_WithExistingId_ReturnsOkAndSoftDeletes()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProductModifications.Add(MakeMod(1007, 1007));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemWrite");

        // Act
        var response = await client.DeleteAsync("/api/product-modification/1007");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await assertDb.ProductModifications.FindAsync(1007);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteProductModification_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemWrite");

        // Act
        var response = await client.DeleteAsync("/api/product-modification/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET LINKED ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProductModificationsLinkedToCartItem_WithAnyId_ReturnsOkWithEmptyList()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product-modification/cart-item/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductModificationResponse>>(_jsonOptions);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProductModificationsLinkedToProduct_WithAnyId_ReturnsOkWithEmptyList()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/product-modification/product/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductModificationResponse>>(_jsonOptions);
        body!.TotalCount.Should().Be(0);
    }
}
