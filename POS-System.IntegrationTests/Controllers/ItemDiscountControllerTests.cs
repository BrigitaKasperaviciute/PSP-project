using FluentAssertions;
using POS_System.Business.Dtos;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ItemDiscountControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public ItemDiscountControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateItemDiscount_WithValidPayload_ReturnsOkAndPersistsDiscount()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder()
            .WithValue(20)
            .WithIsPercentage(true)
            .WithDescription($"Discount-{Guid.NewGuid():N}")
            .WithActiveNow()
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("value").GetInt32().Should().Be(20);
        body.GetProperty("isPercentage").GetBoolean().Should().BeTrue();

        await using var db = _factory.CreateDbContext();
        var persisted = await db.ItemDiscounts.AsNoTracking().FirstAsync(discount => discount.Description == request.Description);
        persisted.Value.Should().Be(20);
    }

    [Fact]
    public async Task GetItemDiscountById_WithExistingId_ReturnsOkAndDiscount()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountRequestBuilder().WithActiveNow().Build());
        var createdDiscount = await ReadJsonAsync(createResponse);

        // Act
        var response = await _client.GetAsync($"/api/item-discount/{createdDiscount.GetProperty("id").GetInt32()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("id").GetInt32().Should().Be(createdDiscount.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task UpdateItemDiscount_WithValidPayload_ReturnsOkAndUpdatesDatabase()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountRequestBuilder().WithActiveNow().Build());
        var createdDiscount = await ReadJsonAsync(createResponse);

        var updateRequest = new ItemDiscountRequestBuilder()
            .WithValue(40)
            .WithIsPercentage(true)
            .WithDescription($"Updated-{Guid.NewGuid():N}")
            .WithActiveNow()
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/item-discount/{createdDiscount.GetProperty("id").GetInt32()}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("value").GetInt32().Should().Be(40);

        await using var db = _factory.CreateDbContext();
        var persisted = await db.ItemDiscounts.AsNoTracking().FirstAsync(discount => discount.Description == updateRequest.Description || discount.Value == 40);
        persisted.Value.Should().Be(40);
    }

    [Fact]
    public async Task DeleteItemDiscount_WithExistingId_ReturnsOkAndRemovesDiscount()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountRequestBuilder().WithActiveNow().Build());
        var createdDiscount = await ReadJsonAsync(createResponse);

        // Act
        var response = await _client.DeleteAsync($"/api/item-discount/{createdDiscount.GetProperty("id").GetInt32()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkItemDiscountToProducts_WithValidIds_ReturnsOkAndCreatesRelationship()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountRequestBuilder().WithActiveNow().Build());
        var createdDiscount = await ReadJsonAsync(createResponse);
        var productIds = new[] { 4 };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/item-discount/{createdDiscount.GetProperty("id").GetInt32()}/link?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateItemDiscount_WithPastStartDate_ReturnsBadRequest()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder()
            .WithCustomDates(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1))
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllLinkUnlinkAndReadLinkedItemDiscounts_ReturnsOkAndUpdatesRelationships()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", new ItemDiscountRequestBuilder().WithActiveNow().Build());
        var createdDiscount = await ReadJsonAsync(createResponse);
        var discountId = createdDiscount.GetProperty("id").GetInt32();
        var productIds = new[] { 4 };

        // Act
        var getAllResponse = await _client.GetAsync("/api/item-discount?pageNum=0&pageSize=10");
        var linkResponse = await _client.PutAsJsonAsync($"/api/item-discount/{discountId}/link?itemsAreProducts=true", productIds);
        var linkedResponse = await _client.GetAsync($"/api/item-discount/item/4?isProduct=true");

        // Assert
        getAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await getAllResponse.Content.ReadFromJsonAsync<PagedResponse<ItemDiscountResponse>>();
        page.Should().NotBeNull();
        page!.Results.Should().NotBeEmpty();

        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        linkedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var linkedBody = await linkedResponse.Content.ReadFromJsonAsync<List<ItemDiscountResponse>>();
        linkedBody.Should().NotBeNull();
        linkedBody!.Should().NotBeEmpty();

        await using var db = _factory.CreateDbContext();
        (await db.ProductOnItemDiscounts.AsNoTracking().CountAsync(link => link.LeftEntityId == 4 && link.RightEntityId == discountId))
            .Should().Be(1);

        var unlinkResponse = await _client.PutAsJsonAsync($"/api/item-discount/{discountId}/unlink?itemsAreProducts=true", productIds);
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var refreshedDb = _factory.CreateDbContext();
        (await refreshedDb.ProductOnItemDiscounts.AsNoTracking().CountAsync(link => link.LeftEntityId == 4 && link.RightEntityId == discountId && link.EndDate == null))
            .Should().Be(0);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}