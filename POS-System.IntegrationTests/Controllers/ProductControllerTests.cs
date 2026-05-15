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
        await db.ProductModifications.ExecuteDeleteAsync();
        await db.CartItems.Where(ci => ci.ProductVersionId != null).ExecuteDeleteAsync();
        await db.ProductOnTaxes.ExecuteDeleteAsync();
        await db.ProductOnItemDiscounts.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithItemReadClaim_ReturnsOkAndPagedProducts()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build());

        // Act
        var response = await _client.GetAsync("/api/product?pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedProductResponseDto<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/product");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectProduct()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().WithName("TestWidget").WithPrice(2000).Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _client.GetAsync($"/api/product/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body!.Name.Should().Be("TestWidget");
        body.Price.Should().Be(2000);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/product/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- GetVersions ---

    [Fact]
    public async Task GetVersions_WithValidProductId_ReturnsVersions()
    {
        // Arrange – create a product, update it to generate a second version
        var created = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();
        await _client.PutAsJsonAsync($"/api/product/{created!.Id}", new ProductBuilder().WithName("UpdatedName").Build());

        // Act
        var response = await _client.GetAsync($"/api/product/{created.ProductId}/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Count().Should().BeGreaterThanOrEqualTo(2);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsProduct()
    {
        // Arrange
        var request = new ProductBuilder().WithName("NewGadget").WithPrice(3500).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/product", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body!.Name.Should().Be("NewGadget");
        body.Price.Should().Be(3500);
        body.IsDeleted.Should().BeFalse();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("NewGadget");
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        var readOnlyClient = _factory.CreateClientWithClaims("ItemRead");
        var response = await readOnlyClient.PostAsJsonAsync("/api/product", new ProductBuilder().Build());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_CreatesNewVersionAndSoftDeletesOld()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().WithName("OldProduct").Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/product/{created!.Id}", new ProductBuilder().WithName("NewProduct").Build());

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body!.Name.Should().Be("NewProduct");
        body.ProductId.Should().Be(created.ProductId);

        // Assert - old version soft-deleted
        await using var db = _factory.CreateDbContext();
        var oldVersion = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == created.Id);
        oldVersion!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/product/999999", new ProductBuilder().Build());
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
        var response = await _client.DeleteAsync("/api/product/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GetLinkedToTax ---

    [Fact]
    public async Task GetLinkedToTax_WithLinkedProduct_ReturnsOkAndContainsProduct()
    {
        // Arrange
        var product = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();

        var taxClient = _factory.CreateClientWithClaims("TaxRead", "TaxWrite");
        var tax = await (await taxClient.PostAsJsonAsync("/api/tax", new TaxBuilder().Build()))
            .Content.ReadFromJsonAsync<TaxResponse>();
        await taxClient.PutAsJsonAsync($"/api/tax/{tax!.Id}/link?itemsAreProducts=true", new[] { product!.Id });

        // Act
        var response = await _client.GetAsync($"/api/product/tax/{tax.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        body!.Should().Contain(p => p.Id == product.Id);
    }

    // --- GetLinkedToItemDiscount ---

    [Fact]
    public async Task GetLinkedToItemDiscount_WithLinkedProduct_ReturnsOkAndContainsProduct()
    {
        // Arrange
        var product = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();

        var discountClient = _factory.CreateClientWithClaims("ItemDiscountRead", "ItemDiscountWrite");
        var discount = await (await discountClient.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();
        await discountClient.PutAsJsonAsync(
            $"/api/item-discount/{discount!.Id}/link?itemsAreProducts=true",
            new[] { product!.Id });

        // Act
        var response = await _client.GetAsync($"/api/product/item-discount/{discount.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        body!.Should().Contain(p => p.Id == product.Id);
    }
}

file record PagedProductResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
