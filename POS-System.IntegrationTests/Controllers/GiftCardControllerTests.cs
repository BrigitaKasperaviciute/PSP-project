using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Business.Dtos;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class GiftCardControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public GiftCardControllerTests(ApiTestFactory factory)
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
    public async Task GetAllGiftCards_WithActiveGiftCards_ReturnsOkAndPagedResults()
    {
        // Arrange
        var firstGiftCard = await CreateGiftCardAsync(200);
        var secondGiftCard = await CreateGiftCardAsync(350);

        // Act
        var response = await _client.GetAsync("/api/giftcards?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<GiftCardResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().Contain(giftCard => giftCard.Id == firstGiftCard.Id);
        body.Results.Should().Contain(giftCard => giftCard.Id == secondGiftCard.Id);
    }

    [Fact]
    public async Task GetGiftCardById_WithExistingId_ReturnsOkAndGiftCard()
    {
        // Arrange
        var createdGiftCard = await CreateGiftCardAsync(500);

        // Act
        var response = await _client.GetAsync($"/api/giftcards/{createdGiftCard.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdGiftCard.Id);
        body.Value.Should().Be(500);
    }

    [Fact]
    public async Task UpdateGiftCard_WithValidPayload_ReturnsOkAndUpdatesDatabase()
    {
        // Arrange
        var createdGiftCard = await CreateGiftCardAsync(500);
        var updateRequest = new GiftCardRequestBuilder()
            .WithDate(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)))
            .WithValue(900)
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/giftcards/{createdGiftCard.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(900);

        await using var db = _factory.CreateDbContext();
        var persisted = await db.GiftCards.AsNoTracking().SingleAsync(giftCard => giftCard.Value == 900 && giftCard.Date == updateRequest.Date);
        persisted.Value.Should().Be(900);
    }

    [Fact]
    public async Task DeleteGiftCard_WithExistingId_ReturnsNoContentAndRemovesGiftCard()
    {
        // Arrange
        var createdGiftCard = await CreateGiftCardAsync(650);

        // Act
        var response = await _client.DeleteAsync($"/api/giftcards/{createdGiftCard.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = _factory.CreateDbContext();
        var deleted = await db.GiftCards.AsNoTracking().SingleOrDefaultAsync(giftCard => giftCard.Value == createdGiftCard.Value && giftCard.Date == createdGiftCard.Date);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task CreateGiftCard_WithPastDate_ReturnsBadRequest()
    {
        // Arrange
        var request = new GiftCardRequestBuilder()
            .WithDate(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/giftcards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<GiftCardResponse> CreateGiftCardAsync(int value)
    {
        var request = new GiftCardRequestBuilder()
            .WithDate(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)))
            .WithValue(value)
            .Build();

        var response = await _client.PostAsJsonAsync("/api/giftcards", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(value);
        return body;
    }
}