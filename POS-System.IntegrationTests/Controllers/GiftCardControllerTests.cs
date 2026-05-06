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
public sealed class GiftCardControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public GiftCardControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.GiftCards.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetAllGiftCards ---------------

    [Fact]
    public async Task GetAllGiftCards_WhenGiftCardsExist_ReturnsOkWithPagedResults()
    {
        // Arrange
        await CreateGiftCardAsync(new DateOnly(2027, 12, 31), 5000);

        // Act
        var response = await _client.GetAsync("/api/giftcards?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<GiftCardResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAllGiftCards_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/giftcards?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- GetGiftCardById ---------------

    [Fact]
    public async Task GetGiftCardById_WhenGiftCardExists_ReturnsOkWithGiftCard()
    {
        // Arrange
        var created = await CreateGiftCardAsync(new DateOnly(2027, 6, 30), 10000);

        // Act
        var response = await _client.GetAsync($"/api/giftcards/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.Value.Should().Be(10000);
    }

    [Fact]
    public async Task GetGiftCardById_WhenGiftCardDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string nonExistentId = "non-existent-gc-id-99999";

        // Act
        var response = await _client.GetAsync($"/api/giftcards/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- CreateGiftCard ---------------

    [Fact]
    public async Task CreateGiftCard_WithValidRequest_ReturnsOkAndPersistsGiftCard()
    {
        // Arrange
        var request = new GiftCardRequest
        {
            Date = new DateOnly(2027, 1, 1),
            Value = 2500
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/giftcards", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(request.Value);
        body.Date.Should().Be(request.Date);
        body.Id.Should().NotBe(0);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.GiftCards.AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == body.Id.ToString());
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(request.Value);
    }

    [Fact]
    public async Task CreateGiftCard_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();
        var request = new GiftCardRequest { Date = new DateOnly(2027, 1, 1), Value = 100 };

        // Act
        var response = await unauthClient.PostAsJsonAsync("/api/giftcards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- UpdateGiftCard ---------------

    [Fact]
    public async Task UpdateGiftCard_WithValidRequest_ReturnsOkAndUpdates()
    {
        // Arrange
        var created = await CreateGiftCardAsync(new DateOnly(2027, 3, 1), 1000);
        var updateRequest = new GiftCardRequest { Date = new DateOnly(2027, 6, 1), Value = 9999 };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/giftcards/{created.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(updateRequest.Value);
        body.Date.Should().Be(updateRequest.Date);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.GiftCards.AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == created.Id.ToString());
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(updateRequest.Value);
    }

    [Fact]
    public async Task UpdateGiftCard_WhenGiftCardDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var request = new GiftCardRequest { Date = new DateOnly(2027, 1, 1), Value = 100 };

        // Act
        var response = await _client.PutAsJsonAsync("/api/giftcards/non-existent-99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- DeleteGiftCard ---------------

    [Fact]
    public async Task DeleteGiftCard_WhenGiftCardExists_ReturnsNoContentAndRemovesFromDb()
    {
        // Arrange
        var created = await CreateGiftCardAsync(new DateOnly(2027, 1, 1), 500);

        // Act
        var response = await _client.DeleteAsync($"/api/giftcards/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – removed from database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.GiftCards.AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == created.Id.ToString());
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteGiftCard_WhenGiftCardDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string nonExistentId = "not-a-real-gift-card-id";

        // Act
        var response = await _client.DeleteAsync($"/api/giftcards/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- helpers ----

    private async Task<GiftCardResponse> CreateGiftCardAsync(DateOnly date, int value)
    {
        var response = await _client.PostAsJsonAsync("/api/giftcards",
            new GiftCardRequest { Date = date, Value = value });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GiftCardResponse>())!;
    }
}
