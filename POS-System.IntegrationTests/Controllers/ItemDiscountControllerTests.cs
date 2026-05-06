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
public sealed class ItemDiscountControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ItemDiscountControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    // Seeded item discounts have Ids 1–4
    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ItemDiscounts.Where(id => id.Id > 4).ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetAllItemDiscounts ---------------

    [Fact]
    public async Task GetAllItemDiscounts_WhenDiscountsExist_ReturnsOkWithPagedResults()
    {
        // Arrange – all seeded discounts are either deleted or expired (EndDate < UtcNow);
        // create one with null dates so GetAllItemDiscounts filter passes it through
        var createResp = await _client.PostAsJsonAsync("/api/item-discount",
            new ItemDiscountRequest { Value = 5, IsPercentage = true, Description = "Active", StartDate = null, EndDate = null });
        createResp.EnsureSuccessStatusCode();

        // Act
        var response = await _client.GetAsync("/api/item-discount?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ItemDiscountResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllItemDiscounts_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/item-discount?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- GetItemDiscountById ---------------

    [Fact]
    public async Task GetItemDiscountById_WhenDiscountExists_ReturnsOkWithDiscount()
    {
        // Arrange – seeded ItemDiscount Id=2 (Value=15, IsPercentage=true, IsDeleted=false)
        const int existingId = 2;

        // Act
        var response = await _client.GetAsync($"/api/item-discount/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.Value.Should().Be(15);
    }

    [Fact]
    public async Task GetItemDiscountById_WhenDiscountDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/item-discount/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- CreateItemDiscount ---------------

    [Fact]
    public async Task CreateItemDiscount_WithValidRequest_ReturnsOkAndPersists()
    {
        // Arrange – StartDate must be strictly >= UtcNow at validation time; use AddDays(1) to avoid race
        var request = new ItemDiscountRequest
        {
            Value = 10,
            IsPercentage = true,
            Description = "Summer sale",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(request.Value);
        body.Description.Should().Be(request.Description);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.ItemDiscounts.AsNoTracking()
            .SingleOrDefaultAsync(id => id.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(request.Value);
        persisted.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateItemDiscount_WithMissingDescription_ReturnsBadRequest()
    {
        // Arrange – Description is required
        var payload = new
        {
            Value = 10,
            IsPercentage = true,
            StartDate = (DateTime?)null,
            EndDate = (DateTime?)null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --------------- UpdateItemDiscountById ---------------

    [Fact]
    public async Task UpdateItemDiscountById_WithValidRequest_ReturnsOkWithNewVersion()
    {
        // Arrange
        var created = await CreateItemDiscountAsync(5, true, "Old discount");
        var updateRequest = new ItemDiscountRequest
        {
            Value = 25,
            IsPercentage = false,
            Description = "Updated discount",
            StartDate = null,
            EndDate = null
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/item-discount/{created.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(updateRequest.Value);
        body.Description.Should().Be(updateRequest.Description);

        // Assert – versioning: new version row created
        await using var db = _factory.CreateDbContext();
        var versions = await db.ItemDiscounts.AsNoTracking()
            .Where(id => id.ItemDiscountId == created.ItemDiscountId)
            .ToListAsync();
        versions.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task UpdateItemDiscountById_WhenDiscountDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var request = new ItemDiscountRequest
        {
            Value = 5,
            IsPercentage = true,
            Description = "X",
            StartDate = null,
            EndDate = null
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/item-discount/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- DeleteItemDiscountById ---------------

    [Fact]
    public async Task DeleteItemDiscountById_WhenDiscountExists_ReturnsOkAndSoftDeletes()
    {
        // Arrange
        var created = await CreateItemDiscountAsync(8, true, "Delete me");

        // Act
        var response = await _client.DeleteAsync($"/api/item-discount/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – soft-deleted
        await using var db = _factory.CreateDbContext();
        var persisted = await db.ItemDiscounts.AsNoTracking()
            .SingleOrDefaultAsync(id => id.Id == created.Id);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteItemDiscountById_WhenDiscountDoesNotExist_ReturnsNotFound()
    {
        // Arrange

        // Act
        var response = await _client.DeleteAsync("/api/item-discount/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- LinkItemDiscountToItems ---------------

    [Fact]
    public async Task LinkItemDiscountToItems_WithValidProductIds_ReturnsOk()
    {
        // Arrange – seeded ItemDiscount Id=2 (IsDeleted=false), seeded Product Id=4
        const int discountId = 2;
        var productIds = new[] { 4 };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/item-discount/{discountId}/link?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkItemDiscountToItems_WithNonExistentDiscount_ReturnsOk()
    {
        // Arrange
        var productIds = new[] { 4 };

        // Act
        var response = await _client.PutAsJsonAsync(
            "/api/item-discount/99999/link?itemsAreProducts=true", productIds);

        // Assert – ManyToManyService silently does nothing when discount not found, returns 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --------------- UnlinkItemDiscountFromItems ---------------

    [Fact]
    public async Task UnlinkItemDiscountFromItems_AfterLinking_ReturnsOk()
    {
        // Arrange – link seeded Product Id=4 to seeded ItemDiscount Id=3, then unlink
        const int discountId = 3;
        var productIds = new[] { 4 };
        await _client.PutAsJsonAsync($"/api/item-discount/{discountId}/link?itemsAreProducts=true", productIds);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/item-discount/{discountId}/unlink?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlinkItemDiscountFromItems_WhenDiscountDoesNotExist_ReturnsOk()
    {
        // Arrange
        var productIds = new[] { 4 };

        // Act
        var response = await _client.PutAsJsonAsync(
            "/api/item-discount/99999/unlink?itemsAreProducts=true", productIds);

        // Assert – ManyToManyService silently does nothing when discount not found, returns 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --------------- GetItemDiscountsLinkedToItemId ---------------

    [Fact]
    public async Task GetItemDiscountsLinkedToItemId_WhenProductExists_ReturnsOk()
    {
        // Arrange – seeded Product Id=4 exists
        const int productId = 4;

        // Act
        var response = await _client.GetAsync($"/api/item-discount/item/{productId}?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- helpers ----

    private async Task<ItemDiscountResponse> CreateItemDiscountAsync(
        int value, bool isPercentage, string description)
    {
        var response = await _client.PostAsJsonAsync("/api/item-discount",
            new ItemDiscountRequest
            {
                Value = value,
                IsPercentage = isPercentage,
                Description = description,
                StartDate = null,
                EndDate = null
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ItemDiscountResponse>())!;
    }
}
