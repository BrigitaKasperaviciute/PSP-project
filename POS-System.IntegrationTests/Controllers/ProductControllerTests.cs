using System.Net;
using Microsoft.AspNetCore.Http;
using Xunit;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Infrastructure;
using System.Collections.Generic;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ProductControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ProductControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    // Seeded products have Ids 1–4
    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.Products.Where(p => p.Id > 4).ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetAllProducts ---------------

    [Fact]
    public async Task GetAllProducts_WhenProductsExist_ReturnsOkWithPagedResults()
    {
        // Arrange – seeded products are present

        // Act
        var response = await _client.GetAsync("/api/product?pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllProducts_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/product?pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- GetProductById ---------------

    [Fact]
    public async Task GetProductById_WhenProductExists_ReturnsOkWithProduct()
    {
        // Arrange – seeded Product Id=4 (ProductId=1, Name="Product1 v3", IsDeleted=false)
        const int existingId = 4;

        // Act
        var response = await _client.GetAsync($"/api/product/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.Name.Should().Be("Product1 v3");
    }

    [Fact]
    public async Task GetProductById_WhenProductDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/product/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- CreateProduct ---------------

    [Fact]
    public async Task CreateProduct_WithValidRequest_ReturnsOkAndPersistsProduct()
    {
        // Arrange – ImageURL must be a valid http/https URL (ProductRequestValidator)
        var request = new ProductRequest
        {
            Name = "Widget Pro",
            Description = "A great widget",
            Price = 999,
            ImageURL = "https://example.com/widget.png",
            Stock = 50
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/product", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(request.Name);
        body.Price.Should().Be(request.Price);
        body.Stock.Should().Be(request.Stock);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(request.Name);
        persisted.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProduct_WithMissingRequiredField_ReturnsBadRequest()
    {
        // Arrange – omit Name
        var payload = new { Description = "desc", Price = 100, ImageURL = "", Stock = 5 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/product", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --------------- UpdateProductByProductId ---------------

    [Fact]
    public async Task UpdateProductByProductId_WithValidRequest_ReturnsOkWithNewVersion()
    {
        // Arrange
        var created = await CreateProductAsync("UpdateMe", "desc", 100, "https://example.com/update.png", 10);

        var updateRequest = new ProductRequest
        {
            Name = "UpdatedWidget",
            Description = "Updated desc",
            Price = 1299,
            ImageURL = "https://example.com/updated.png",
            Stock = 20
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/product/{created.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(updateRequest.Name);
        body.Price.Should().Be(updateRequest.Price);

        // Assert – versioning: a new row was created for the same ProductId
        await using var db = _factory.CreateDbContext();
        var versions = await db.Products.AsNoTracking()
            .Where(p => p.ProductId == created.ProductId)
            .ToListAsync();
        versions.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task UpdateProductByProductId_WhenProductDoesNotExist_ReturnsNotFound()
    {
        // Arrange – need valid ImageURL so validator passes and request reaches business logic
        var request = new ProductRequest { Name = "X", Description = "x", Price = 1, ImageURL = "https://example.com/x.png", Stock = 1 };

        // Act
        var response = await _client.PutAsJsonAsync("/api/product/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- DeleteProductById ---------------

    [Fact]
    public async Task DeleteProductById_WhenProductExists_ReturnsOkAndSoftDeletes()
    {
        // Arrange
        var created = await CreateProductAsync("DeleteMe", "desc", 50, "https://example.com/delete.png", 5);

        // Act
        var response = await _client.DeleteAsync($"/api/product/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – soft-deleted
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == created.Id);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteProductById_WhenProductDoesNotExist_ReturnsNotFound()
    {
        // Arrange

        // Act
        var response = await _client.DeleteAsync("/api/product/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- GetProductVersionsByProductId ---------------

    [Fact]
    public async Task GetProductVersionsByProductId_WhenVersionsExist_ReturnsOk()
    {
        // Arrange – seeded product ProductId=1 has multiple versions (Ids 1, 3, 4)
        const int productId = 1;

        // Act
        var response = await _client.GetAsync($"/api/product/{productId}/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetProductVersionsByProductId_WhenProductDoesNotExist_ReturnsOkWithEmptyList()
    {
        // Arrange
        const int nonExistentProductId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/product/{nonExistentProductId}/versions/");

        // Assert – service checks for null (not empty), returns 200 with empty list for unknown ProductId
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    // --------------- GetProductsLinkedToTaxId ---------------

    [Fact]
    public async Task GetProductsLinkedToTaxId_WhenProductsLinked_ReturnsOkWithList()
    {
        // Arrange – link seeded Product Id=4 to seeded Tax Id=2 (active, IsDeleted=false)
        await _client.PutAsJsonAsync("/api/tax/2/link?itemsAreProducts=true", new[] { 4 });

        // Act
        var response = await _client.GetAsync("/api/product/tax/2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
    }

    // --------------- GetProductsLinkedToItemDiscountId ---------------

    [Fact]
    public async Task GetProductsLinkedToItemDiscountId_WhenProductsLinked_ReturnsOkWithList()
    {
        // Arrange – create a discount with null dates; filter requires both null when timeStamp is null
        var discountResp = await _client.PostAsJsonAsync("/api/item-discount",
            new ItemDiscountRequest { Value = 5, IsPercentage = true, Description = "Test", StartDate = null, EndDate = null });
        discountResp.EnsureSuccessStatusCode();
        var discount = (await discountResp.Content.ReadFromJsonAsync<ItemDiscountResponse>())!;
        await _client.PutAsJsonAsync($"/api/item-discount/{discount.Id}/link?itemsAreProducts=true", new[] { 4 });

        // Act
        var response = await _client.GetAsync($"/api/product/item-discount/{discount.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
    }

    // ---- helpers ----

    private async Task<ProductResponse> CreateProductAsync(
        string name, string description, int price, string imageUrl, int stock)
    {
        var url = string.IsNullOrEmpty(imageUrl) ? "https://example.com/product.png" : imageUrl;
        var response = await _client.PostAsJsonAsync("/api/product",
            new ProductRequest
            {
                Name = name,
                Description = description,
                Price = price,
                ImageURL = url,
                Stock = stock
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }
}
