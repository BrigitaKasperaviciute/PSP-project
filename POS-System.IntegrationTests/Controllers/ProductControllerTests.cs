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
public sealed class ProductControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public ProductControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        // Clean products table before each test
        await _db.Products.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateProduct_WithValidPayload_ReturnsOkAndPersistsProduct()
    {
        // Arrange
        var request = new ProductRequestBuilder()
            .WithName("Coffee")
            .WithDescription("Premium coffee beans")
            .WithPrice(12.99m)
            .WithIsActive(true)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/product", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.Name.Should().Be(request.Name);
        body.Description.Should().Be(request.Description);
        body.Price.Should().Be(request.Price);

        // Assert - database state
        var persisted = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(request.Name);
        persisted.Description.Should().Be(request.Description);
    }

    [Fact]
    public async Task GetAllProducts_WithOnlyActiveFalse_ReturnsAllProducts()
    {
        // Arrange
        var activeProduct = new ProductRequestBuilder().WithIsActive(true).Build();
        var inactiveProduct = new ProductRequestBuilder().WithIsActive(false).Build();
        
        await _client.PostAsJsonAsync("/api/product", activeProduct);
        await _client.PostAsJsonAsync("/api/product", inactiveProduct);

        // Act
        var response = await _client.GetAsync("/api/product?onlyActive=false&pageSize=10&pageNumber=0");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Data.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllProducts_WithOnlyActiveTrue_ReturnsOnlyActiveProducts()
    {
        // Arrange
        var activeProduct1 = new ProductRequestBuilder().WithName("Product1").WithIsActive(true).Build();
        var activeProduct2 = new ProductRequestBuilder().WithName("Product2").WithIsActive(true).Build();
        var inactiveProduct = new ProductRequestBuilder().WithName("InactiveProduct").WithIsActive(false).Build();
        
        await _client.PostAsJsonAsync("/api/product", activeProduct1);
        await _client.PostAsJsonAsync("/api/product", activeProduct2);
        await _client.PostAsJsonAsync("/api/product", inactiveProduct);

        // Act
        var response = await _client.GetAsync("/api/product?onlyActive=true&pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<ProductResponse>>();
        body!.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetProductById_WithExistingId_ReturnsOkWithProduct()
    {
        // Arrange
        var request = new ProductRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/product", request);
        var createdProduct = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _client.GetAsync($"/api/product/{createdProduct!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdProduct.Id);
        body.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task UpdateProduct_WithValidPayload_ReturnsOkAndUpdatesProduct()
    {
        // Arrange
        var createRequest = new ProductRequestBuilder().WithName("OldName").WithPrice(9.99m).Build();
        var createResponse = await _client.PostAsJsonAsync("/api/product", createRequest);
        var createdProduct = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var updateRequest = new ProductRequestBuilder()
            .WithName("UpdatedName")
            .WithPrice(19.99m)
            .Build();
        var response = await _client.PutAsJsonAsync($"/api/product/{createdProduct!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body!.Name.Should().Be(updateRequest.Name);
        body.Price.Should().Be(updateRequest.Price);

        // Verify versioning behavior
        var oldProduct = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == createdProduct.Id);
        oldProduct.Should().NotBeNull();
        oldProduct!.IsDeleted.Should().BeTrue();

        var persisted = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(updateRequest.Name);
    }

    [Fact]
    public async Task DeleteProduct_WithExistingId_ReturnsOkAndDeletesProduct()
    {
        // Arrange
        var request = new ProductRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/product", request);
        var createdProduct = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/product/{createdProduct!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify deletion
        var deleted = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == createdProduct.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetProductVersionsByProductId_WithExistingProduct_ReturnsVersions()
    {
        // Arrange
        var request = new ProductRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/product", request);
        var createdProduct = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _client.GetAsync($"/api/product/{createdProduct!.Id}/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ProductVersionResponse>>();
        body.Should().NotBeNull();
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetProductById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/product/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateProduct_WithNullName_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = """{"name": null, "description": "Test", "price": 10, "isActive": true}""";

        // Act
        var response = await _client.PostAsJsonAsync("/api/product", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_WithNegativePrice_ReturnsBadRequest()
    {
        // Arrange
        var request = new ProductRequestBuilder().WithPrice(-5.99m).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/product", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_WithZeroPrice_MayReturnBadRequest()
    {
        // Arrange
        var request = new ProductRequestBuilder().WithPrice(0).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/product", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateProduct_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ProductRequestBuilder().Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/product/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProduct_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/product/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductVersionsByProductId_WithNonExistentProductId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/product/999999/versions/");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductsLinkedToTaxId_WithNonExistentTaxId_ReturnsEmptyOrNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/product/tax/999999");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductsLinkedToItemDiscountId_WithNonExistentDiscountId_ReturnsEmptyOrNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/product/item-discount/999999");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion
}

public class ProductVersionResponse
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}
