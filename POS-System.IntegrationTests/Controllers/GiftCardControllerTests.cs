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
public sealed class GiftCardControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public GiftCardControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        await _db.GiftCards.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateGiftCard_WithValidPayload_ReturnsOkAndPersistsGiftCard()
    {
        // Arrange
        var expirationDate = DateTime.UtcNow.AddYears(1);
        var request = new GiftCardRequestBuilder()
            .WithDate(expirationDate)
            .WithValue(100.0m)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/giftcards", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeNullOrEmpty();
        body.Value.Should().Be(request.Value);

        // Assert - database state
        var persisted = await _db.GiftCards.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(request.Value);
    }

    [Fact]
    public async Task GetAllGiftCards_WithValidPageNumbers_ReturnsOkWithGiftCards()
    {
        // Arrange
        var request1 = new GiftCardRequestBuilder().Build();
        var request2 = new GiftCardRequestBuilder().Build();
        await _client.PostAsJsonAsync("/api/giftcards", request1);
        await _client.PostAsJsonAsync("/api/giftcards", request2);

        // Act
        var response = await _client.GetAsync("/api/giftcards?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<GiftCardResponse>>();
        body!.Data.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetGiftCardById_WithExistingId_ReturnsOkWithGiftCard()
    {
        // Arrange
        var request = new GiftCardRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/giftcards", request);
        var createdGiftCard = await createResponse.Content.ReadFromJsonAsync<GiftCardResponse>();

        // Act
        var response = await _client.GetAsync($"/api/giftcards/{createdGiftCard!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body!.Id.Should().Be(createdGiftCard.Id);
    }

    [Fact]
    public async Task UpdateGiftCard_WithValidPayload_ReturnsOkAndUpdatesGiftCard()
    {
        // Arrange
        var createRequest = new GiftCardRequestBuilder().WithValue(50.0m).Build();
        var createResponse = await _client.PostAsJsonAsync("/api/giftcards", createRequest);
        var createdGiftCard = await createResponse.Content.ReadFromJsonAsync<GiftCardResponse>();

        // Act
        var updateRequest = new GiftCardRequestBuilder().WithValue(75.0m).Build();
        var response = await _client.PutAsJsonAsync($"/api/giftcards/{createdGiftCard!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body!.Value.Should().Be(updateRequest.Value);
    }

    [Fact]
    public async Task DeleteGiftCard_WithExistingId_ReturnsOkAndDeletesGiftCard()
    {
        // Arrange
        var request = new GiftCardRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/giftcards", request);
        var createdGiftCard = await createResponse.Content.ReadFromJsonAsync<GiftCardResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/giftcards/{createdGiftCard!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleted = await _db.GiftCards.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == createdGiftCard.Id);
        deleted.Should().BeNull();
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetGiftCardById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/giftcards/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateGiftCard_WithPastExpirationDate_MayReturnBadRequest()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddYears(-1);
        var request = new GiftCardRequestBuilder().WithDate(pastDate).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/giftcards", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateGiftCard_WithNegativeValue_ReturnsBadRequest()
    {
        // Arrange
        var request = new GiftCardRequestBuilder().WithValue(-50.0m).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/giftcards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateGiftCard_WithZeroValue_MayReturnBadRequest()
    {
        // Arrange
        var request = new GiftCardRequestBuilder().WithValue(0).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/giftcards", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateGiftCard_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new GiftCardRequestBuilder().Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/giftcards/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGiftCard_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/giftcards/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

// using POS_System.Business.Dtos.Response.GiftCardResponse
