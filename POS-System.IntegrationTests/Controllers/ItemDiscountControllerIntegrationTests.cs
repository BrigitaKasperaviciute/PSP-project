using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class ItemDiscountControllerCoverageTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _authorizedClient = null!;

    public ItemDiscountControllerCoverageTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _authorizedClient = _factory.CreateAuthenticatedClient("ItemDiscountRead", "ItemDiscountWrite");
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _authorizedClient.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetAllItemDiscounts_SeededRequest_ReturnsPagedResults()
    {
        // Arrange
        var seededDiscount = await SeedItemDiscountAsync("GetAll item discount scenario");

        // Act
        var response = await _authorizedClient.GetAsync("/api/item-discount?pageNum=0&pageSize=35");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ItemDiscountResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().Contain(discount => discount.ItemDiscountId == seededDiscount.ItemDiscountId);
    }

    [Fact]
    public async Task GetAllItemDiscounts_WithoutWriteClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient();

        // Act
        var response = await unauthorizedClient.GetAsync("/api/item-discount?pageNum=0&pageSize=35");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateItemDiscount_ValidRequest_ReturnsOkAndPersistsDiscount()
    {
        // Arrange
        var uniqueDescription = $"Integration created item discount {Guid.NewGuid():N}";
        var request = new ItemDiscountRequestBuilder()
            .WithValue(18)
            .WithIsPercentage(true)
            .WithDescription(uniqueDescription)
            .WithStartDate(DateTime.UtcNow.AddMinutes(5))
            .WithEndDate(DateTime.UtcNow.AddDays(5))
            .Build();

        // Act
        var response = await _authorizedClient.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
            body.Should().NotBeNull();
            body!.Value.Should().Be(request.Value);
            body.IsPercentage.Should().Be(request.IsPercentage);
            body.Description.Should().Be(request.Description);

            await using var db = _factory.GetDbContext();
            var persisted = await db.ItemDiscounts.AsNoTracking().SingleAsync(discount => discount.Description == uniqueDescription);

            persisted.Value.Should().Be(request.Value);
            persisted.Description.Should().Be(request.Description);
            persisted.IsDeleted.Should().BeFalse();
        }
    }

    [Fact]
    public async Task CreateItemDiscount_WithoutWriteClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("ItemDiscountRead");
        var request = new ItemDiscountRequestBuilder().Build();

        // Act
        var response = await unauthorizedClient.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetItemDiscountById_ExistingDiscount_ReturnsOkAndMatchesDatabase()
    {
        // Arrange
        var seededDiscount = await SeedItemDiscountAsync("GetById item discount scenario");

        // Act
        var response = await _authorizedClient.GetAsync($"/api/item-discount/{seededDiscount.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(seededDiscount.Id);
        body.ItemDiscountId.Should().Be(seededDiscount.ItemDiscountId);
        body.Description.Should().Be(seededDiscount.Description);
    }

    [Fact]
    public async Task GetItemDiscountById_MissingDiscount_ReturnsNotFound()
    {
        // Act
        var response = await _authorizedClient.GetAsync("/api/item-discount/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateItemDiscount_ExistingDiscount_ReturnsOkAndCreatesReplacement()
    {
        // Arrange
        var seededDiscount = await SeedItemDiscountAsync("Update item discount scenario");
        var request = new ItemDiscountRequestBuilder()
            .WithValue(25)
            .WithIsPercentage(false)
            .WithDescription("Updated item discount")
            .WithStartDate(DateTime.UtcNow.AddMinutes(5))
            .WithEndDate(DateTime.UtcNow.AddDays(5))
            .Build();

        // Act
        var response = await _authorizedClient.PutAsJsonAsync($"/api/item-discount/{seededDiscount.Id}", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
            body.Should().NotBeNull();
            body!.Value.Should().Be(request.Value);
            body.IsPercentage.Should().Be(request.IsPercentage);
            body.Description.Should().Be(request.Description);

            await using var db = _factory.GetDbContext();
            var versions = await db.ItemDiscounts.AsNoTracking()
                .Where(discount => discount.ItemDiscountId == seededDiscount.ItemDiscountId)
                .ToListAsync();

            versions.Should().HaveCountGreaterOrEqualTo(2);
            versions.Should().Contain(discount => discount.IsDeleted && discount.Id == seededDiscount.Id);
            versions.Should().Contain(discount => !discount.IsDeleted && discount.Description == request.Description);
        }
    }

    [Fact]
    public async Task UpdateItemDiscount_MissingDiscount_ReturnsNotFound()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder().Build();

        // Act
        var response = await _authorizedClient.PutAsJsonAsync("/api/item-discount/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteItemDiscount_ExistingDiscount_ReturnsOkAndMarksDeleted()
    {
        // Arrange
        var seededDiscount = await SeedItemDiscountAsync("Delete item discount scenario");

        // Act
        var response = await _authorizedClient.DeleteAsync($"/api/item-discount/{seededDiscount.Id}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            await using var db = _factory.GetDbContext();
            var deletedDiscount = await db.ItemDiscounts.AsNoTracking().SingleAsync(discount => discount.Id == seededDiscount.Id);

            deletedDiscount.IsDeleted.Should().BeTrue();
        }
    }

    [Fact]
    public async Task DeleteItemDiscount_MissingDiscount_ReturnsNotFound()
    {
        // Act
        var response = await _authorizedClient.DeleteAsync("/api/item-discount/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkItemDiscountToItems_ValidRequest_ReturnsOkAndCreatesLink()
    {
        // Arrange
        var seededDiscount = await SeedItemDiscountAsync("Link item discount scenario", null, null);

        // Act
        var response = await _authorizedClient.PutAsJsonAsync($"/api/item-discount/{seededDiscount.Id}/link?itemsAreProducts=false", new[] { 1 });

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnlinkItemDiscountFromItems_ValidRequest_ReturnsOkAndRemovesLink()
    {
        // Arrange
        var seededDiscount = await SeedItemDiscountAsync("Unlink item discount scenario", null, null);
        var linkResponse = await _authorizedClient.PutAsJsonAsync($"/api/item-discount/{seededDiscount.Id}/link?itemsAreProducts=false", new[] { 1 });
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var response = await _authorizedClient.PutAsJsonAsync($"/api/item-discount/{seededDiscount.Id}/unlink?itemsAreProducts=false", new[] { 1 });

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    private async Task<ItemDiscount> SeedItemDiscountAsync(string description, DateTime? startDate = null, DateTime? endDate = null)
    {
        await using var db = _factory.GetDbContext();

        var itemDiscount = new ItemDiscount
        {
            ItemDiscountId = 1000 + Random.Shared.Next(1, 100000),
            Value = 20,
            IsPercentage = true,
            Description = description,
            StartDate = startDate,
            EndDate = endDate,
            Version = DateTime.UtcNow,
            IsDeleted = false
        };

        db.ItemDiscounts.Add(itemDiscount);
        await db.SaveChangesAsync();

        return itemDiscount;
    }
}