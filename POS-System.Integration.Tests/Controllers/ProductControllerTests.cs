using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

public class ProductControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public ProductControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Seeded: Product Id=4 (ProductId=1, Name="Product1 v3", IsDeleted=false) is the only active product.

    [Fact]
    public async Task GetAll_ValidToken_ReturnsOkWithPagedResult()
    {
        // Arrange
        // (seeded database has active products)

        // Act
        var response = await _authClient.GetAsync("/api/product");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/product");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithProduct()
    {
        // Arrange
        const int existingId = 4; // seeded "Product1 v3"

        // Act
        var response = await _authClient.GetAsync($"/api/product/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.Name.Should().Be("Product1 v3");
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _authClient.GetAsync($"/api/product/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetVersionsByProductId_ExistingProductId_ReturnsVersionList()
    {
        // Arrange
        const int productId = 1; // seeded ProductId=1 has multiple versions (Ids 1, 3, 4)

        // Act
        var response = await _authClient.GetAsync($"/api/product/{productId}/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task GetVersionsByProductId_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/product/1/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithCreatedProduct()
    {
        // Arrange
        var request = new ProductRequest
        {
            Name = "IntegrationProduct",
            Description = "Test product",
            Price = 999,
            ImageURL = "http://example.com/img.png",
            Stock = 50
        };

        // Act
        var response = await _authClient.PostAsJsonAsync("/api/product", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("IntegrationProduct");
        body.Price.Should().Be(999);
        body.Stock.Should().Be(50);
        body.IsDeleted.Should().BeFalse();

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Products.FirstOrDefaultAsync(p => p.Name == "IntegrationProduct");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ProductRequest
        {
            Name = "NoAuthProduct",
            Description = "x",
            Price = 1,
            ImageURL = "",
            Stock = 1
        };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/product", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ExistingId_ReturnsOkWithUpdatedProduct()
    {
        // Arrange – create a product to update
        var created = await (await _authClient.PostAsJsonAsync("/api/product",
            new ProductRequest { Name = "ProductToUpdate", Description = "d", Price = 100, ImageURL = "http://example.com/img.png", Stock = 5 }))
            .Content.ReadFromJsonAsync<ProductResponse>();
        var updateRequest = new ProductRequest
        {
            Name = "ProductUpdated",
            Description = "updated desc",
            Price = 200,
            ImageURL = "http://example.com/img.png",
            Stock = 10
        };

        // Act
        var response = await _authClient.PutAsJsonAsync($"/api/product/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("ProductUpdated");
        body.Price.Should().Be(200);
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ProductRequest { Name = "x", Description = "x", Price = 1, ImageURL = "http://example.com/img.png", Stock = 1 };

        // Act
        var response = await _authClient.PutAsJsonAsync("/api/product/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsOkWithSoftDeletedProduct()
    {
        // Arrange – create a product to delete
        var created = await (await _authClient.PostAsJsonAsync("/api/product",
            new ProductRequest { Name = "ProductToDelete", Description = "d", Price = 50, ImageURL = "http://example.com/img.png", Stock = 1 }))
            .Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _authClient.DeleteAsync($"/api/product/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.IsDeleted.Should().BeTrue();

        // Verify soft-delete persisted
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await db.Products.FindAsync(created.Id);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        // (nothing to set up)

        // Act
        var response = await _authClient.DeleteAsync("/api/product/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductsLinkedToTaxId_ValidTaxId_ReturnsOkWithList()
    {
        // Arrange
        const int taxId = 2; // seeded tax

        // Act
        var response = await _authClient.GetAsync($"/api/product/tax/{taxId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProductsLinkedToItemDiscountId_ValidId_ReturnsOkWithList()
    {
        // Arrange
        const int itemDiscountId = 2; // seeded active item discount

        // Act
        var response = await _authClient.GetAsync($"/api/product/item-discount/{itemDiscountId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProductsLinkedToTaxId_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/product/tax/2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
