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

public class ProductModificationControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public ProductModificationControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Seeded active ProductModifications (IsDeleted=false):
    //   Id=2 (ProductModificationId=1, "Extra cheese v2", ProductVersionId=1)
    //   Id=4 (ProductModificationId=2, "No cheese v2",    ProductVersionId=1)
    //   Id=5 (ProductModificationId=3, "Extra fork",      ProductVersionId=2)

    [Fact]
    public async Task GetAll_ValidToken_ReturnsOkWithPagedResult()
    {
        // Arrange
        // (seeded modifications)

        // Act
        var response = await _authClient.GetAsync("/api/product-modification");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/product-modification");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithModification()
    {
        // Arrange
        const int existingId = 2; // seeded "Extra cheese v2"

        // Act
        var response = await _authClient.GetAsync($"/api/product-modification/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.Name.Should().Be("Extra cheese v2");
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _authClient.GetAsync($"/api/product-modification/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetVersionsByModificationId_ExistingProductModificationId_ReturnsVersionList()
    {
        // Arrange – ProductModificationId=1 has two versions (Id=1 IsDeleted=true, Id=2 active)
        const int modificationId = 1; // seeded ProductModificationId=1

        // Act
        var response = await _authClient.GetAsync($"/api/product-modification/{modificationId}/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GetVersionsByModificationId_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/product-modification/1/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithCreatedModification()
    {
        // Arrange
        var request = new ProductModificationRequest
        {
            ProductVersionId = 4, // seeded active product version
            Name = "Integration Modification",
            Description = "Added in integration test",
            Price = 50
        };

        // Act
        var response = await _authClient.PostAsJsonAsync("/api/product-modification", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Integration Modification");
        body.Price.Should().Be(50);
        body.IsDeleted.Should().BeFalse();

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.ProductModifications.FirstOrDefaultAsync(m => m.Name == "Integration Modification");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ProductModificationRequest
        {
            ProductVersionId = 4,
            Name = "NoAuth",
            Description = "x",
            Price = 1
        };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/product-modification", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ExistingId_ReturnsOkWithUpdatedModification()
    {
        // Arrange – create a modification to update
        var created = await (await _authClient.PostAsJsonAsync("/api/product-modification",
            new ProductModificationRequest { ProductVersionId = 4, Name = "ModToUpdate", Description = "d", Price = 10 }))
            .Content.ReadFromJsonAsync<ProductModificationResponse>();
        var updateReq = new ProductModificationRequest { ProductVersionId = 4, Name = "ModUpdated", Description = "new desc", Price = 99 };

        // Act
        var response = await _authClient.PutAsJsonAsync($"/api/product-modification/{created!.Id}", updateReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("ModUpdated");
        body.Price.Should().Be(99);
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ProductModificationRequest { ProductVersionId = 4, Name = "x", Description = "x", Price = 1 };

        // Act
        var response = await _authClient.PutAsJsonAsync("/api/product-modification/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsOkWithSoftDeletedModification()
    {
        // Arrange – create a dedicated modification to delete
        var created = await (await _authClient.PostAsJsonAsync("/api/product-modification",
            new ProductModificationRequest { ProductVersionId = 4, Name = "ModToDelete", Description = "d", Price = 1 }))
            .Content.ReadFromJsonAsync<ProductModificationResponse>();

        // Act
        var response = await _authClient.DeleteAsync($"/api/product-modification/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.IsDeleted.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await db.ProductModifications.FindAsync(created.Id);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        // (nothing to set up)

        // Act
        var response = await _authClient.DeleteAsync("/api/product-modification/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetModificationsLinkedToProductId_ValidProductId_ReturnsOkWithList()
    {
        // Arrange
        const int productVersionId = 1; // seeded product version with linked modifications

        // Act
        var response = await _authClient.GetAsync($"/api/product-modification/product/{productVersionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetModificationsLinkedToCartItemId_ValidCartItemId_ReturnsOkWithList()
    {
        // Arrange
        const int cartItemId = 1; // seeded cart item

        // Act
        var response = await _authClient.GetAsync($"/api/product-modification/cart-item/{cartItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductModificationResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetModificationsLinkedToProductId_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/product-modification/product/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
