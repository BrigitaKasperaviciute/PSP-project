using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class GiftCardControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;
    private readonly HttpClient _writeClient;
    private readonly HttpClient _anonClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GiftCardControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _readClient  = factory.CreateClientWithClaims("GiftCardRead");
        _writeClient = factory.CreateClientWithClaims("GiftCardRead", "GiftCardWrite");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Helper: create a valid gift card and return its response
    private async Task<GiftCardResponse> CreateGiftCardAsync(DateOnly expiryDate, int value = 50)
    {
        var request = new GiftCardRequest { Date = expiryDate, Value = value };
        var response = await _writeClient.PostAsJsonAsync("/api/giftcards", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GiftCardResponse>(body, JsonOptions)!;
    }

    // ── POST /api/giftcards ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsOkAndPersistsGiftCard()
    {
        // Arrange — future expiry so the card is considered valid
        var expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6));
        var request = new GiftCardRequest { Date = expiryDate, Value = 100 };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/giftcards", request);
        var body = await response.Content.ReadAsStringAsync();
        var giftCard = JsonSerializer.Deserialize<GiftCardResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        giftCard.Should().NotBeNull();
        giftCard!.Value.Should().Be(100);
        giftCard.Id.Should().BeGreaterThan(0);

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.GiftCards.FindAsync(giftCard.Id.ToString());
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(100);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new GiftCardRequest { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), Value = 10 };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/giftcards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/giftcards ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithGiftCardReadClaim_ReturnsOkWithPagedResult()
    {
        // Arrange — create one valid (non-expired) gift card first
        await CreateGiftCardAsync(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)));

        // Act
        var response = await _readClient.GetAsync("/api/giftcards");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<GiftCardResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyNonExpiredGiftCards()
    {
        // Arrange — create an expired card (date in the past) and a valid one
        var expiredRequest = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Value = 20
        };
        await _writeClient.PostAsJsonAsync("/api/giftcards", expiredRequest);
        await CreateGiftCardAsync(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), 30);

        // Act
        var response = await _readClient.GetAsync("/api/giftcards");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<GiftCardResponse>>(body, JsonOptions);

        // Assert — the service filters by Date >= today, so expired cards must not appear
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged!.Results.Should().OnlyContain(gc =>
            gc.Date >= DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── GET /api/giftcards/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithGiftCard()
    {
        // Arrange — create then retrieve
        var created = await CreateGiftCardAsync(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)));

        // Act
        var response = await _readClient.GetAsync($"/api/giftcards/{created.Id}");
        var body = await response.Content.ReadAsStringAsync();
        var giftCard = JsonSerializer.Deserialize<GiftCardResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        giftCard!.Id.Should().Be(created.Id);
        giftCard.Value.Should().Be(created.Value);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _readClient.GetAsync("/api/giftcards/nonexistent-id-00000000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/giftcards/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task Update_WithExistingId_ReturnsOkWithUpdatedGiftCard()
    {
        // Arrange
        var created = await CreateGiftCardAsync(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)), 50);
        var updateRequest = new GiftCardRequest
        {
            Date  = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(4)),
            Value = 200
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync($"/api/giftcards/{created.Id}", updateRequest);
        var body = await response.Content.ReadAsStringAsync();
        var updated = JsonSerializer.Deserialize<GiftCardResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated!.Value.Should().Be(200);

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.GiftCards.FindAsync(created.Id.ToString());
        persisted!.Value.Should().Be(200);
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new GiftCardRequest
        {
            Date  = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Value = 50
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/giftcards/no-such-id", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/giftcards/{id} ────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingId_ReturnsNoContentAndRemovesGiftCard()
    {
        // Arrange
        var created = await CreateGiftCardAsync(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)));

        // Act
        var response = await _writeClient.DeleteAsync($"/api/giftcards/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – removed from database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.GiftCards.FindAsync(created.Id.ToString());
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _writeClient.DeleteAsync("/api/giftcards/no-such-card");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
