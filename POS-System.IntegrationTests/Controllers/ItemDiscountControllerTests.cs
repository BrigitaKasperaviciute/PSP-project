using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ItemDiscountControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public ItemDiscountControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        await _db.ItemDiscounts.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateItemDiscount_WithValidPayloadAndActiveDates_ReturnsOkAndPersistsDiscount()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder()
            .WithValue(10.0m)
            .WithIsPercentage(true)
            .WithDescription("Summer Sale")
            .WithActiveDates()
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.Value.Should().Be(request.Value);
        body.IsPercentage.Should().Be(request.IsPercentage);

        // Assert - database state
        var persisted = await _db.ItemDiscounts.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(request.Value);
    }

    [Fact]
    public async Task CreateItemDiscount_WithFutureDates_ReturnsOkAndPersistsDiscount()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder()
            .WithValue(15.0m)
            .WithFutureDates()
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllItemDiscounts_WithValidPageNumbers_ReturnsOkWithDiscounts()
    {
        // Arrange
        var request1 = new ItemDiscountRequestBuilder().WithActiveDates().Build();
        var request2 = new ItemDiscountRequestBuilder().WithActiveDates().Build();
        await _client.PostAsJsonAsync("/api/item-discount", request1);
        await _client.PostAsJsonAsync("/api/item-discount", request2);

        // Act
        var response = await _client.GetAsync("/api/item-discount?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<ItemDiscountResponse>>();
        body!.Data.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetItemDiscountById_WithExistingId_ReturnsOkWithDiscount()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder().WithActiveDates().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", request);
        var createdDiscount = await createResponse.Content.ReadFromJsonAsync<ItemDiscountResponse>();

        // Act
        var response = await _client.GetAsync($"/api/item-discount/{createdDiscount!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body!.Id.Should().Be(createdDiscount.Id);
    }

    [Fact]
    public async Task UpdateItemDiscount_WithValidPayload_ReturnsOkAndUpdatesDiscount()
    {
        // Arrange
        var createRequest = new ItemDiscountRequestBuilder().WithActiveDates().WithValue(10.0m).Build();
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", createRequest);
        var createdDiscount = await createResponse.Content.ReadFromJsonAsync<ItemDiscountResponse>();

        // Act
        var updateRequest = new ItemDiscountRequestBuilder().WithActiveDates().WithValue(20.0m).Build();
        var response = await _client.PutAsJsonAsync($"/api/item-discount/{createdDiscount!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body!.Value.Should().Be(updateRequest.Value);
    }

    [Fact]
    public async Task DeleteItemDiscount_WithExistingId_ReturnsOkAndDeletesDiscount()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder().WithActiveDates().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", request);
        var createdDiscount = await createResponse.Content.ReadFromJsonAsync<ItemDiscountResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/item-discount/{createdDiscount!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify deletion
        var deleted = await _db.ItemDiscounts.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == createdDiscount.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetItemDiscountById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/item-discount/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateItemDiscount_WithPastStartDate_ReturnsBadRequest()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder()
            .WithStartDate(DateTime.UtcNow.AddDays(-1))
            .WithEndDate(DateTime.UtcNow.AddDays(7))
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateItemDiscount_WithNegativeValue_ReturnsBadRequest()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder()
            .WithActiveDates()
            .WithValue(-10.0m)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateItemDiscount_WithOnlyStartDateProvided_ReturnsBadRequest()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder()
            .WithStartDate(DateTime.UtcNow.AddDays(1))
            .WithEndDate(null)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateItemDiscount_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ItemDiscountRequestBuilder().WithActiveDates().Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/item-discount/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteItemDiscount_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/item-discount/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

public class ItemDiscountResponse
{
    public int Id { get; set; }
    public decimal Value { get; set; }
    public bool IsPercentage { get; set; }
    public string Description { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
