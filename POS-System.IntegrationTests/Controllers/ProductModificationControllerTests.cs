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

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ProductModificationControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ProductModificationControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    // Seeded product modifications have Ids 1–5
    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ProductModifications.Where(pm => pm.Id > 5).ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetAllProductModifications ---------------

    [Fact]
    public async Task GetAllProductModifications_WhenModificationsExist_ReturnsOkWithPagedResults()
    {
        // Arrange – seeded modifications are present

        // Act
        var response = await _client.GetAsync("/api/product-modification?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllProductModifications_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/product-modification?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- GetProductModificationById ---------------

    [Fact]
    public async Task GetProductModificationById_WhenModificationExists_ReturnsOkWithModification()
    {
        // Arrange – seeded ProductModification Id=2 (Name="Extra cheese v2", IsDeleted=false)
        const int existingId = 2;

        // Act
        var response = await _client.GetAsync($"/api/product-modification/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.Name.Should().Be("Extra cheese v2");
    }

    [Fact]
    public async Task GetProductModificationById_WhenModificationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/product-modification/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- CreateProductModification ---------------

    [Fact]
    public async Task CreateProductModification_WithValidRequest_ReturnsOkAndPersists()
    {
        // Arrange – seeded product version Id=1
        var request = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name = "Extra sauce",
            Description = "Hot sauce addition",
            Price = 50
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/product-modification", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(request.Name);
        body.Price.Should().Be(request.Price);
        body.ProductVersionId.Should().Be(request.ProductVersionId);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.ProductModifications.AsNoTracking()
            .SingleOrDefaultAsync(pm => pm.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProductModification_WithMissingName_ReturnsBadRequest()
    {
        // Arrange – Name is required
        var payload = new { ProductVersionId = 1, Description = "desc", Price = 50 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/product-modification", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --------------- UpdateProductModification ---------------

    [Fact]
    public async Task UpdateProductModification_WithValidRequest_ReturnsOkWithNewVersion()
    {
        // Arrange
        var created = await CreateModificationAsync(productVersionId: 1, "Old topping", "desc", 30);
        var updateRequest = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name = "New topping",
            Description = "Updated desc",
            Price = 75
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/product-modification/{created.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(updateRequest.Name);
        body.Price.Should().Be(updateRequest.Price);

        // Assert – versioning: new row created
        await using var db = _factory.CreateDbContext();
        var versions = await db.ProductModifications.AsNoTracking()
            .Where(pm => pm.ProductModificationId == created.ProductModificationId)
            .ToListAsync();
        versions.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task UpdateProductModification_WhenModificationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var request = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name = "X",
            Description = "x",
            Price = 10
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/product-modification/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- DeleteProductModification ---------------

    [Fact]
    public async Task DeleteProductModification_WhenModificationExists_ReturnsOkAndSoftDeletes()
    {
        // Arrange
        var created = await CreateModificationAsync(productVersionId: 1, "DeleteMe", "desc", 20);

        // Act
        var response = await _client.DeleteAsync($"/api/product-modification/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – soft-deleted
        await using var db = _factory.CreateDbContext();
        var persisted = await db.ProductModifications.AsNoTracking()
            .SingleOrDefaultAsync(pm => pm.Id == created.Id);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteProductModification_WhenModificationDoesNotExist_ReturnsNotFound()
    {
        // Arrange

        // Act
        var response = await _client.DeleteAsync("/api/product-modification/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- GetProductModificationVersionsByProductModificationId ---------------

    [Fact]
    public async Task GetVersionsByProductModificationId_WhenVersionsExist_ReturnsOk()
    {
        // Arrange – seeded ProductModificationId=1 has Ids 1 and 2
        const int modificationId = 1;

        // Act
        var response = await _client.GetAsync($"/api/product-modification/{modificationId}/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetVersionsByProductModificationId_WhenModificationDoesNotExist_ReturnsOkWithEmptyList()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/product-modification/{nonExistentId}/versions/");

        // Assert – service checks for null (not empty), returns 200 with empty list for unknown id
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    // --------------- GetProductModificationsLinkedToCartItemId ---------------

    [Fact]
    public async Task GetProductModificationsLinkedToCartItemId_WhenCartItemHasModifications_ReturnsOk()
    {
        // Arrange – seeded CartItem Id=1 exists (in Cart Id=1)
        const int cartItemId = 1;

        // Act
        var response = await _client.GetAsync($"/api/product-modification/cart-item/{cartItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --------------- GetProductModificationsLinkedToProductId ---------------

    [Fact]
    public async Task GetProductModificationsLinkedToProductId_WhenProductHasModifications_ReturnsOk()
    {
        // Arrange – seeded Product Id=1 (ProductId=1) exists and has modifications
        const int productId = 1;

        // Act
        var response = await _client.GetAsync($"/api/product-modification/product/{productId}?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductModificationResponse>>();
        body.Should().NotBeNull();
    }

    // ---- helpers ----

    private async Task<ProductModificationResponse> CreateModificationAsync(
        int productVersionId, string name, string description, int price)
    {
        var response = await _client.PostAsJsonAsync("/api/product-modification",
            new ProductModificationRequest
            {
                ProductVersionId = productVersionId,
                Name = name,
                Description = description,
                Price = price
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductModificationResponse>())!;
    }
}
