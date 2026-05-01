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

public class ProductControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;   // ItemRead claim
    private readonly HttpClient _writeClient;  // ItemRead + ItemWrite claims
    private readonly HttpClient _anonClient;   // no auth

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ProductControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _readClient  = factory.CreateClientWithClaims("ItemRead");
        _writeClient = factory.CreateClientWithClaims("ItemRead", "ItemWrite");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET /api/product ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithItemReadClaim_ReturnsOkWithPagedResult()
    {
        // Arrange — seed data contains 4 product rows (some deleted, one active)

        // Act
        var response = await _readClient.GetAsync("/api/product");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<ProductResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_WithOnlyActiveFilter_ReturnsOnlyNonDeletedProducts()
    {
        // Arrange — seed has 1 active product (IsDeleted=false, Id=4)

        // Act
        var response = await _readClient.GetAsync("/api/product?onlyActive=true");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<ProductResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged!.Results.Should().OnlyContain(p => !p.IsDeleted);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/product");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/product/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithProduct()
    {
        // Arrange — product Id=4 is active (seeded)

        // Act
        var response = await _readClient.GetAsync("/api/product/4");
        var body = await response.Content.ReadAsStringAsync();
        var product = JsonSerializer.Deserialize<ProductResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        product.Should().NotBeNull();
        product!.Id.Should().Be(4);
        product.Name.Should().Be("Product1 v3");
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _readClient.GetAsync("/api/product/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/product ────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsOkAndPersistsProduct()
    {
        // Arrange
        var request = new ProductRequest
        {
            Name        = "Integration Test Product",
            Description = "Created by integration test",
            Price       = 999,
            ImageURL    = "https://example.com/img.png",
            Stock       = 50
        };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/product", request);
        var body = await response.Content.ReadAsStringAsync();
        var product = JsonSerializer.Deserialize<ProductResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        product.Should().NotBeNull();
        product!.Name.Should().Be("Integration Test Product");
        product.Price.Should().Be(999);
        product.Id.Should().BeGreaterThan(0);

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Products.FindAsync(product.Id);
        persisted.Should().NotBeNull();
        persisted!.Stock.Should().Be(50);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ProductRequest
        {
            Name = "Unauthorized", Description = "Test",
            Price = 1, ImageURL = "", Stock = 1
        };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/product", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/product/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithExistingId_ReturnsOkWithUpdatedProduct()
    {
        // Arrange — product Id=4 is the active seeded product
        var request = new ProductRequest
        {
            Name        = "Updated Product Name",
            Description = "Updated desc",
            Price       = 750,
            ImageURL    = "https://example.com/product.jpg",
            Stock       = 20
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/product/4", request);
        var body = await response.Content.ReadAsStringAsync();
        var product = JsonSerializer.Deserialize<ProductResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        product!.Name.Should().Be("Updated Product Name");
        product.Price.Should().Be(750);

        // Assert – database reflects the new version
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var latest = db.Products.OrderByDescending(p => p.Version)
                                .FirstOrDefault(p => p.ProductId == 1);
        latest!.Name.Should().Be("Updated Product Name");
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ProductRequest
        {
            Name = "Ghost", Description = "Ghost", Price = 1, ImageURL = "https://example.com/ghost.jpg", Stock = 1
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/product/9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/product/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndSoftDeletesProduct()
    {
        // Arrange — product Id=4 is active
        var beforeResponse = await _readClient.GetAsync("/api/product/4");
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var response = await _writeClient.DeleteAsync("/api/product/4");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – product is soft-deleted in the database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.FindAsync(4);
        product!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _writeClient.DeleteAsync("/api/product/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
