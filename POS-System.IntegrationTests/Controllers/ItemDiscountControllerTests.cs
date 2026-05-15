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
        await db.ItemDiscounts.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithItemDiscountReadClaim_ReturnsOk()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build());

        // Act
        var response = await _client.GetAsync("/api/item-discount?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedItemDiscountResponseDto<ItemDiscountResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/item-discount");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectDiscount()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/item-discount",
                new ItemDiscountBuilder().WithValue(15).Build()))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();

        // Act
        var response = await _client.GetAsync($"/api/item-discount/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body!.Value.Should().Be(15);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/item-discount/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsDiscount()
    {
        // Arrange
        var request = new ItemDiscountBuilder().WithValue(20).WithIsPercentage(true).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body!.Value.Should().Be(20);
        body.IsPercentage.Should().BeTrue();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.ItemDiscounts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == body.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        var readOnlyClient = _factory.CreateClientWithClaims("ItemDiscountRead");
        var response = await readOnlyClient.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_CreatesNewVersionAndSoftDeletesOld()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/item-discount",
                new ItemDiscountBuilder().WithValue(10).Build()))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/item-discount/{created!.Id}",
            new ItemDiscountBuilder().WithValue(25).Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body!.Value.Should().Be(25);
        body.ItemDiscountId.Should().Be(created.ItemDiscountId);

        // Assert - old version soft-deleted
        await using var db = _factory.CreateDbContext();
        var oldVersion = await db.ItemDiscounts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == created.Id);
        oldVersion!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/item-discount/999999", new ItemDiscountBuilder().Build());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOk()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/item-discount/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/item-discount/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Link / Unlink ---

    [Fact]
    public async Task LinkToProduct_ThenGetLinkedDiscounts_ReturnsLinkedDiscount()
    {
        // Arrange
        var discount = await (await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();

        var productClient = _factory.CreateClientWithClaims("ItemRead", "ItemWrite");
        var product = await (await productClient.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();

        // Act - link
        var linkResponse = await _client.PutAsJsonAsync(
            $"/api/item-discount/{discount!.Id}/link?itemsAreProducts=true",
            new[] { product!.Id });
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - query linked discounts
        var getResponse = await _client.GetAsync($"/api/item-discount/item/{product.Id}?isProduct=true");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadFromJsonAsync<IEnumerable<ItemDiscountResponse>>();
        body!.Should().Contain(d => d.Id == discount.Id);
    }

    [Fact]
    public async Task UnlinkFromProduct_RemovesLink()
    {
        // Arrange – create a product, discount, and link them
        var productClient = _factory.CreateClientWithClaims("ItemRead", "ItemWrite");
        var product = await (await productClient.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();
        var discount = await (await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();
        await _client.PutAsJsonAsync(
            $"/api/item-discount/{discount!.Id}/link?itemsAreProducts=true",
            new[] { product!.Id });

        // Act – unlink
        var response = await _client.PutAsJsonAsync(
            $"/api/item-discount/{discount.Id}/unlink?itemsAreProducts=true",
            new[] { product.Id });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

file record PagedItemDiscountResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
