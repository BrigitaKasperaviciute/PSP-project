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
public sealed class ProductModificationControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;
    private int _testProductVersionId = 0;

    public ProductModificationControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        await _db.ProductModifications.ExecuteDeleteAsync();
            await _db.ProductModifications.ExecuteDeleteAsync();
        await _db.Products.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();

        // Create test product and get its version
        var productRequest = new ProductRequestBuilder().Build();
        var productResponse = await _client.PostAsJsonAsync("/api/product", productRequest);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();

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
    public async Task CreateProductModification_WithValidPayload_ReturnsOkAndPersistsModification()
    {
        // Arrange
        var request = new ProductModificationRequestBuilder()
            .WithProductVersionId(_testProductVersionId)
            .WithName("Extra Cheese")
            .WithDescription("Add extra cheese")
            .WithPrice(2.50m)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/product-modification", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.Name.Should().Be(request.Name);

        // Assert - database state
        var persisted = await _db.ProductModifications.AsNoTracking()
            .FirstOrDefaultAsync(pm => pm.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task GetAllProductModifications_WithValidPageNumbers_ReturnsOkWithModifications()
    {
        // Arrange
        var request1 = new ProductModificationRequestBuilder()
            .WithProductVersionId(_testProductVersionId)
            .Build();
        
        await _client.PostAsJsonAsync("/api/product-modification", request1);

        // Act
        var response = await _client.GetAsync("/api/product-modification?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<ProductModificationResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProductModificationById_WithExistingId_ReturnsOkWithModification()
    {
        // Arrange
        var createRequest = new ProductModificationRequestBuilder()
            .WithProductVersionId(_testProductVersionId)
            .Build();
        
        var createResponse = await _client.PostAsJsonAsync("/api/product-modification", createRequest);
        if (!createResponse.IsSuccessStatusCode)
        {
            var error = await createResponse.Content.ReadAsStringAsync();
            throw new Exception($"Create product modification failed {(int)createResponse.StatusCode}: {error}");
        }
        var createdModification = await createResponse.Content.ReadFromJsonAsync<ProductModificationResponse>();
        var persistedModification = await _db.ProductModifications.AsNoTracking()
            .FirstAsync(pm => pm.ProductVersionId == _testProductVersionId && pm.Name == createRequest.Name);

        // Act
        var response = await _client.GetAsync($"/api/product-modification/{persistedModification.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body!.Id.Should().Be(persistedModification.Id);
    }

    [Fact]
    public async Task UpdateProductModification_WithValidPayload_ReturnsOkAndUpdatesModification()
    {
        // Arrange
        var createRequest = new ProductModificationRequestBuilder()
            .WithProductVersionId(_testProductVersionId)
            .WithName("OldName")
            .Build();
        
        var createResponse = await _client.PostAsJsonAsync("/api/product-modification", createRequest);
        var createdModification = await createResponse.Content.ReadFromJsonAsync<ProductModificationResponse>();
        var persistedModification = await _db.ProductModifications.AsNoTracking()
            .FirstAsync(pm => pm.ProductVersionId == _testProductVersionId && pm.Name == createRequest.Name);

        // Act
        var updateRequest = new ProductModificationRequestBuilder()
            .WithProductVersionId(_testProductVersionId)
            .WithName("UpdatedName")
            .Build();
        
        var response = await _client.PutAsJsonAsync($"/api/product-modification/{persistedModification.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body!.Name.Should().Be(updateRequest.Name);
    }

    [Fact]
    public async Task DeleteProductModification_WithExistingId_ReturnsOkAndDeletesModification()
    {
        // Arrange
        var createRequest = new ProductModificationRequestBuilder()
            .WithProductVersionId(_testProductVersionId)
            .Build();
        
        var createResponse = await _client.PostAsJsonAsync("/api/product-modification", createRequest);
        if (!createResponse.IsSuccessStatusCode)
        {
            var error = await createResponse.Content.ReadAsStringAsync();
            throw new Exception($"Create product modification failed {(int)createResponse.StatusCode}: {error}");
        }
        var createdModification = await createResponse.Content.ReadFromJsonAsync<ProductModificationResponse>();
        var persistedModification = await _db.ProductModifications.AsNoTracking()
            .FirstAsync(pm => pm.ProductVersionId == _testProductVersionId && pm.Name == createRequest.Name);

        // Act
        var response = await _client.DeleteAsync($"/api/product-modification/{persistedModification.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify deletion
        var deleted = await _db.ProductModifications.AsNoTracking()
            .FirstOrDefaultAsync(pm => pm.Id == persistedModification.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetProductModificationById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/product-modification/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateProductModification_WithNullName_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = """{"productVersionId": 1, "name": null, "description": "Test", "price": 2.50}""";

        // Act
        var response = await _client.PostAsJsonAsync("/api/product-modification", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProductModification_WithNegativePrice_ReturnsBadRequest()
    {
        // Arrange
        var request = new ProductModificationRequestBuilder()
            .WithProductVersionId(_testProductVersionId)
            .WithPrice(-2.50m)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/product-modification", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProductModification_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ProductModificationRequestBuilder()
            .WithProductVersionId(_testProductVersionId)
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/product-modification/999999", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteProductModification_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/product-modification/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

public class ProductModificationResponse
{
    public int Id { get; set; }
    public int ProductVersionId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
}
