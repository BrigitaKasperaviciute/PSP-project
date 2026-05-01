using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

/// <summary>
/// Seeded product modifications (only active / non-deleted listed):
///   Id=2  ProductModificationId=1  "Extra cheese v2"  Price=100  IsDeleted=false  ProductVersionId=1
///   Id=4  ProductModificationId=2  "No cheese v2"     Price=0    IsDeleted=false  ProductVersionId=1
///   Id=5  ProductModificationId=3  "Extra fork"       Price=50   IsDeleted=false  ProductVersionId=2
/// </summary>
public class ProductModificationControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;
    private readonly HttpClient _writeClient;
    private readonly HttpClient _anonClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ProductModificationControllerTests(PosSystemApiFactory factory)
    {
        _factory     = factory;
        _readClient  = factory.CreateClientWithClaims("ItemRead");
        _writeClient = factory.CreateClientWithClaims("ItemRead", "ItemWrite");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Helper
    private async Task<ProductModificationResponse> CreateModificationAsync(string name = "New Topping")
    {
        var request = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name             = name,
            Description      = "Created by integration test",
            Price            = 75
        };
        var response = await _writeClient.PostAsJsonAsync("/api/product-modification", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ProductModificationResponse>(body, JsonOptions)!;
    }

    // ── GET /api/product-modification ────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithItemReadClaim_ReturnsOkWithPagedResult()
    {
        // Arrange — 3 active modifications seeded

        // Act
        var response = await _readClient.GetAsync("/api/product-modification");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<ProductModificationResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/product-modification");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/product-modification/{id} ───────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithModification()
    {
        // Arrange — modification Id=2 is active ("Extra cheese v2")

        // Act
        var response = await _readClient.GetAsync("/api/product-modification/2");
        var body = await response.Content.ReadAsStringAsync();
        var mod = JsonSerializer.Deserialize<ProductModificationResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mod.Should().NotBeNull();
        mod!.Id.Should().Be(2);
        mod.Name.Should().Be("Extra cheese v2");
        mod.Price.Should().Be(100);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _readClient.GetAsync("/api/product-modification/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/product-modification ───────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsOkAndPersistsModification()
    {
        // Arrange
        var request = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name             = "Extra bacon",
            Description      = "Crunchy bacon strips",
            Price            = 150
        };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/product-modification", request);
        var body = await response.Content.ReadAsStringAsync();
        var mod = JsonSerializer.Deserialize<ProductModificationResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mod.Should().NotBeNull();
        mod!.Name.Should().Be("Extra bacon");
        mod.Price.Should().Be(150);

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.ProductModifications.FindAsync(mod.Id);
        persisted.Should().NotBeNull();
        persisted!.Description.Should().Be("Crunchy bacon strips");
    }

    [Fact]
    public async Task Create_WithoutItemWriteClaim_ReturnsForbidden()
    {
        // Arrange — read-only client
        var request = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name             = "Forbidden Topping",
            Description      = "Test",
            Price            = 10
        };

        // Act
        var response = await _readClient.PostAsJsonAsync("/api/product-modification", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/product-modification/{id} ───────────────────────────────────

    [Fact]
    public async Task Update_WithExistingId_ReturnsOkAndCreatesNewVersion()
    {
        // Arrange — update modification Id=5 ("Extra fork", ProductModificationId=3)
        var request = new ProductModificationRequest
        {
            ProductVersionId = 2,
            Name             = "Extra fork updated",
            Description      = "Updated by integration test",
            Price            = 75
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/product-modification/5", request);
        var body = await response.Content.ReadAsStringAsync();
        var mod = JsonSerializer.Deserialize<ProductModificationResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mod!.Name.Should().Be("Extra fork updated");
        mod.Price.Should().Be(75);

        // Assert – newest active version in DB reflects the update
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var latest = await db.ProductModifications
            .Where(m => m.ProductModificationId == 3 && !m.IsDeleted)
            .OrderByDescending(m => m.Version)
            .FirstOrDefaultAsync();
        latest.Should().NotBeNull();
        latest!.Name.Should().Be("Extra fork updated");
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name             = "Ghost",
            Description      = "Ghost",
            Price            = 1
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/product-modification/9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/product-modification/{id} ────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndSoftDeletesModification()
    {
        // Arrange — create a fresh modification to avoid affecting other test scenarios
        var created = await CreateModificationAsync("Modification To Delete");

        // Act
        var response = await _writeClient.DeleteAsync($"/api/product-modification/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – soft-deleted in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.ProductModifications.FindAsync(created.Id);
        persisted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _writeClient.DeleteAsync("/api/product-modification/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/product-modification/product/{id} ────────────────────────────

    [Fact]
    public async Task GetByProductId_WithExistingProductVersion_ReturnsOkWithModifications()
    {
        // Arrange — product version Id=1 has "Extra cheese v2" and "No cheese v2" seeded

        // Act
        var response = await _readClient.GetAsync("/api/product-modification/product/1");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<ProductModificationResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().BeGreaterThan(0);
    }
}
