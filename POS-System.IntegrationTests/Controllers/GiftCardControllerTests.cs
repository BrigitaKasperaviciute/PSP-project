using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class GiftCardControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public GiftCardControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.GiftCards.RemoveRange(db.GiftCards.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllGiftCards_WhenActiveCardsExist_ReturnsOkWithCards()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        db.GiftCards.Add(new GiftCard { Id = "10001001", Date = futureDate, Value = 5000 });
        db.GiftCards.Add(new GiftCard { Id = "10001002", Date = futureDate, Value = 10000 });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("GiftCardRead");

        // Act
        var response = await client.GetAsync("/api/giftcards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<GiftCardResponse>>(_jsonOptions);
        body!.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        body.Results.Should().Contain(g => g.Value == 5000);
    }

    [Fact]
    public async Task GetAllGiftCards_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/giftcards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGiftCardById_WithExistingActiveId_ReturnsOkWithGiftCard()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        db.GiftCards.Add(new GiftCard { Id = "20002001", Date = futureDate, Value = 2500 });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("GiftCardRead");

        // Act
        var response = await client.GetAsync("/api/giftcards/20002001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>(_jsonOptions);
        body!.Value.Should().Be(2500);
    }

    [Fact]
    public async Task GetGiftCardById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("GiftCardRead");

        // Act
        var response = await client.GetAsync("/api/giftcards/99999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGiftCard_WithValidRequest_ReturnsOkWithCreatedGiftCard()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("GiftCardWrite");
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        var request = new GiftCardRequest { Date = futureDate, Value = 7500 };

        // Act
        var response = await client.PostAsJsonAsync("/api/giftcards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>(_jsonOptions);
        body!.Value.Should().Be(7500);
        body.Id.Should().BeGreaterThan(0);

        // Verify database state
        using var assertScope = _factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.GiftCards.FindAsync(body.Id.ToString());
        saved.Should().NotBeNull();
        saved!.Value.Should().Be(7500);
    }

    [Fact]
    public async Task CreateGiftCard_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new GiftCardRequest { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Value = 1000 };

        // Act
        var response = await client.PostAsJsonAsync("/api/giftcards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateGiftCard_WithExistingId_ReturnsOkWithUpdatedGiftCard()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        db.GiftCards.Add(new GiftCard { Id = "30003001", Date = futureDate, Value = 1000 });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("GiftCardWrite");
        var newFutureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2));
        var request = new GiftCardRequest { Date = newFutureDate, Value = 5000 };

        // Act
        var response = await client.PutAsJsonAsync("/api/giftcards/30003001", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>(_jsonOptions);
        body!.Value.Should().Be(5000);

        // Verify database state
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = await assertDb.GiftCards.FindAsync("30003001");
        updated!.Value.Should().Be(5000);
    }

    [Fact]
    public async Task UpdateGiftCard_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("GiftCardWrite");
        var request = new GiftCardRequest { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Value = 100 };

        // Act
        var response = await client.PutAsJsonAsync("/api/giftcards/99999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteGiftCard_WithExistingId_ReturnsNoContent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.GiftCards.Add(new GiftCard { Id = "40004001", Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Value = 500 });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("GiftCardWrite");

        // Act
        var response = await client.DeleteAsync("/api/giftcards/40004001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify removal from database
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await assertDb.GiftCards.FindAsync("40004001");
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteGiftCard_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("GiftCardWrite");

        // Act
        var response = await client.DeleteAsync("/api/giftcards/99999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
