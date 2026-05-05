using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;

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
        _client = factory.CreateClientWithClaims("ItemRead", "ItemWrite");
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.CartItems.ExecuteDeleteAsync();
        await db.ProductModifications.ExecuteDeleteAsync();
        await db.ProductOnTaxes.ExecuteDeleteAsync();
        await db.ProductOnItemDiscounts.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithValidAuth_ReturnsOkWithProducts()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build());

        // Act
        var response = await _client.GetAsync("/api/product?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/product");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectProduct()
    {
        // Arrange
        var request = new ProductBuilder().WithName("SpecificProduct").WithPrice(1499).Build();
        var created = await (await _client.PostAsJsonAsync("/api/product", request))
            .Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _client.GetAsync($"/api/product/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("SpecificProduct");
        body.Price.Should().Be(1499);
        body.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/product/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsProduct()
    {
        // Arrange
        var request = new ProductBuilder().WithName("NewProduct").WithPrice(599).WithStock(25).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/product", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("NewProduct");
        body.Price.Should().Be(599);
        body.Stock.Should().Be(25);
        body.IsDeleted.Should().BeFalse();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("NewProduct");
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        // Arrange
        var readOnlyClient = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await readOnlyClient.PostAsJsonAsync("/api/product", new ProductBuilder().Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_CreatesNewVersionAndSoftDeletesOld()
    {
        // Arrange
        var original = new ProductBuilder().WithName("OriginalProduct").WithPrice(100).Build();
        var created = await (await _client.PostAsJsonAsync("/api/product", original))
            .Content.ReadFromJsonAsync<ProductResponse>();

        var updateRequest = new ProductBuilder().WithName("UpdatedProduct").WithPrice(200).Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/product/{created!.Id}", updateRequest);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body!.Name.Should().Be("UpdatedProduct");
        body.Price.Should().Be(200);
        body.ProductId.Should().Be(created.ProductId);

        // Assert - old version is soft-deleted
        await using var db = _factory.CreateDbContext();
        var oldVersion = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == created.Id);
        oldVersion!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.PutAsJsonAsync("/api/product/999999", new ProductBuilder().Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndSoftDeletesProduct()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/product/{created!.Id}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database: product is soft-deleted
        await using var db = _factory.CreateDbContext();
        var inDb = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == created.Id);
        inDb!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync("/api/product/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GetVersions ---

    [Fact]
    public async Task GetVersions_WithExistingProduct_ReturnsAllVersions()
    {
        // Arrange – create and update to generate two versions
        var created = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();
        await _client.PutAsJsonAsync($"/api/product/{created!.Id}", new ProductBuilder().WithName("V2").Build());

        // Act
        var response = await _client.GetAsync($"/api/product/{created.ProductId}/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var versions = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        versions.Should().HaveCountGreaterThanOrEqualTo(2);
    }
}

file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
