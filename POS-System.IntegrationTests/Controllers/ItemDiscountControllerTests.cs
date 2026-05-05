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
public sealed class ItemDiscountControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ItemDiscountControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClientWithClaims("ItemDiscountRead", "ItemDiscountWrite", "ItemRead", "ItemWrite");
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ProductOnItemDiscounts.ExecuteDeleteAsync();
        await db.ServiceOnItemDiscounts.ExecuteDeleteAsync();
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.CartItems.ExecuteDeleteAsync();
        await db.ProductModifications.ExecuteDeleteAsync();
        await db.ProductOnTaxes.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.ItemDiscounts.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithValidAuth_ReturnsOkAndPagedDiscounts()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build());

        // Act
        var response = await _client.GetAsync("/api/item-discount?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<ItemDiscountResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/item-discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectDiscount()
    {
        // Arrange
        var request = new ItemDiscountBuilder().WithValue(25).WithDescription("Summer Sale").Build();
        var created = await (await _client.PostAsJsonAsync("/api/item-discount", request))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();

        // Act
        var response = await _client.GetAsync($"/api/item-discount/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(25);
        body.Description.Should().Be("Summer Sale");
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/item-discount/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsDiscount()
    {
        // Arrange
        var request = new ItemDiscountBuilder().WithValue(15).WithIsPercentage(true).WithDescription("Holiday").Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(15);
        body.Description.Should().Be("Holiday");
        body.IsPercentage.Should().BeTrue();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.ItemDiscounts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(15);
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        // Arrange
        var readOnlyClient = _factory.CreateClientWithClaims("ItemDiscountRead");

        // Act
        var response = await readOnlyClient.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_CreatesNewVersionAndSoftDeletesOld()
    {
        // Arrange
        var original = new ItemDiscountBuilder().WithValue(10).WithDescription("Original").Build();
        var created = await (await _client.PostAsJsonAsync("/api/item-discount", original))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();

        var updateRequest = new ItemDiscountBuilder().WithValue(20).WithDescription("Updated").Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/item-discount/{created!.Id}", updateRequest);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body!.Value.Should().Be(20);
        body.Description.Should().Be("Updated");
        body.ItemDiscountId.Should().Be(created.ItemDiscountId);

        // Assert - old version soft-deleted
        await using var db = _factory.CreateDbContext();
        var oldVersion = await db.ItemDiscounts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == created.Id);
        oldVersion!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.PutAsJsonAsync("/api/item-discount/999999", new ItemDiscountBuilder().Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndSoftDeletesDiscount()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/item-discount/{created!.Id}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database: discount is soft-deleted
        await using var db = _factory.CreateDbContext();
        var inDb = await db.ItemDiscounts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == created.Id);
        inDb!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync("/api/item-discount/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Link / Unlink ---

    [Fact]
    public async Task Link_ItemDiscountToProduct_ReturnsOkAndCreatesAssociation()
    {
        // Arrange
        var discount = await (await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();
        var product = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/item-discount/{discount!.Id}/link?itemsAreProducts=true",
            new[] { product!.Id });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // ProductOnItemDiscount inherits BaseManyToManyEntity<Product, ItemDiscount>: LeftEntityId=ProductId, RightEntityId=ItemDiscountId
        await using var db = _factory.CreateDbContext();
        var link = await db.ProductOnItemDiscounts.AsNoTracking()
            .AnyAsync(p => p.RightEntityId == discount.Id && p.LeftEntityId == product.Id);
        link.Should().BeTrue();
    }

    [Fact]
    public async Task Unlink_ItemDiscountFromProduct_ReturnsOkAndRemovesAssociation()
    {
        // Arrange
        var discount = await (await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();
        var product = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();
        await _client.PutAsJsonAsync(
            $"/api/item-discount/{discount!.Id}/link?itemsAreProducts=true",
            new[] { product!.Id });

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/item-discount/{discount.Id}/unlink?itemsAreProducts=true",
            new[] { product.Id });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _factory.CreateDbContext();
        var linkExists = await db.ProductOnItemDiscounts.AsNoTracking()
            .AnyAsync(p => p.RightEntityId == discount.Id && p.LeftEntityId == product.Id && p.EndDate == null);
        linkExists.Should().BeFalse();
    }

    [Fact]
    public async Task GetDiscountsLinkedToItem_WithLinkedProduct_ReturnsDiscounts()
    {
        // Arrange
        var discount = await (await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();
        var product = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();
        await _client.PutAsJsonAsync(
            $"/api/item-discount/{discount!.Id}/link?itemsAreProducts=true",
            new[] { product!.Id });

        // Act
        var response = await _client.GetAsync($"/api/item-discount/item/{product.Id}?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var discounts = await response.Content.ReadFromJsonAsync<IEnumerable<ItemDiscountResponse>>();
        discounts.Should().Contain(d => d.Id == discount.Id);
    }
}

file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
