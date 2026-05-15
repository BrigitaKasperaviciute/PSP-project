using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

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
        _client = factory.CreateClientWithClaims("ItemRead", "ItemWrite");
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.ProductModifications.ExecuteDeleteAsync();
        await db.CartItems.Where(ci => ci.ProductVersionId != null).ExecuteDeleteAsync();
        await db.ProductOnTaxes.ExecuteDeleteAsync();
        await db.ProductOnItemDiscounts.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ProductResponse> CreateProductAsync()
        => (await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>())!;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithItemReadClaim_ReturnsOk()
    {
        // Arrange
        var product = await CreateProductAsync();
        await _client.PostAsJsonAsync("/api/product-modification",
            new ProductModificationBuilder().WithProductVersionId(product.Id).Build());

        // Act
        var response = await _client.GetAsync("/api/product-modification");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedProductModificationResponse<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/product-modification");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectModification()
    {
        // Arrange
        var product = await CreateProductAsync();
        var created = await (await _client.PostAsJsonAsync("/api/product-modification",
                new ProductModificationBuilder().WithProductVersionId(product.Id).WithName("ExtraShot").WithPrice(300).Build()))
            .Content.ReadFromJsonAsync<ProductModificationResponse>();

        // Act
        var response = await _client.GetAsync($"/api/product-modification/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body!.Name.Should().Be("ExtraShot");
        body.Price.Should().Be(300);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/product-modification/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersists()
    {
        // Arrange
        var product = await CreateProductAsync();
        var request = new ProductModificationBuilder().WithProductVersionId(product.Id).WithName("NoSugar").WithPrice(0).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/product-modification", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body!.Name.Should().Be("NoSugar");
        body.ProductVersionId.Should().Be(product.Id);

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.ProductModifications.AsNoTracking().SingleOrDefaultAsync(m => m.Id == body.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        var product = await CreateProductAsync();
        var readOnlyClient = _factory.CreateClientWithClaims("ItemRead");
        var response = await readOnlyClient.PostAsJsonAsync("/api/product-modification",
            new ProductModificationBuilder().WithProductVersionId(product.Id).Build());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_CreatesNewVersionAndSoftDeletesOld()
    {
        // Arrange
        var product = await CreateProductAsync();
        var created = await (await _client.PostAsJsonAsync("/api/product-modification",
                new ProductModificationBuilder().WithProductVersionId(product.Id).WithName("OldMod").Build()))
            .Content.ReadFromJsonAsync<ProductModificationResponse>();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/product-modification/{created!.Id}",
            new ProductModificationBuilder().WithProductVersionId(product.Id).WithName("NewMod").WithPrice(200).Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body!.Name.Should().Be("NewMod");
        body.ProductModificationId.Should().Be(created.ProductModificationId);

        // Assert - old version soft-deleted
        await using var db = _factory.CreateDbContext();
        var oldVersion = await db.ProductModifications.AsNoTracking().SingleOrDefaultAsync(m => m.Id == created.Id);
        oldVersion!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        var product = await CreateProductAsync();
        var response = await _client.PutAsJsonAsync("/api/product-modification/999999",
            new ProductModificationBuilder().WithProductVersionId(product.Id).Build());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOk()
    {
        // Arrange
        var product = await CreateProductAsync();
        var created = await (await _client.PostAsJsonAsync("/api/product-modification",
                new ProductModificationBuilder().WithProductVersionId(product.Id).Build()))
            .Content.ReadFromJsonAsync<ProductModificationResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/product-modification/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/product-modification/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GetByProduct ---

    [Fact]
    public async Task GetByProduct_WithLinkedModification_ReturnsModifications()
    {
        // Arrange
        var product = await CreateProductAsync();
        await _client.PostAsJsonAsync("/api/product-modification",
            new ProductModificationBuilder().WithProductVersionId(product.Id).Build());

        // Act
        var response = await _client.GetAsync($"/api/product-modification/product/{product.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedProductModificationResponse<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }
}

file record PagedProductModificationResponse<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
