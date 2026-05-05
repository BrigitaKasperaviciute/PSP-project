using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
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
        _client = factory.CreateClientWithClaims("GiftCardRead", "GiftCardWrite");
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.GiftCards.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithValidAuth_ReturnsOkAndGiftCards()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/giftcards", new GiftCardBuilder().Build());

        // Act
        var response = await _client.GetAsync("/api/giftcards?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<GiftCardResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/giftcards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectGiftCard()
    {
        // Arrange
        var request = new GiftCardBuilder().WithValue(1000).Build();
        await _client.PostAsJsonAsync("/api/giftcards", request);

        // Retrieve the string ID from the database
        await using var db = _factory.CreateDbContext();
        var giftCard = await db.GiftCards.AsNoTracking().FirstAsync();

        // Act
        var response = await _client.GetAsync($"/api/giftcards/{giftCard.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/giftcards/nonexistent-id-9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsGiftCard()
    {
        // Arrange
        var request = new GiftCardBuilder().WithValue(750).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/giftcards", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var giftCard = await db.GiftCards.AsNoTracking().FirstOrDefaultAsync(g => g.Value == 750);
        giftCard.Should().NotBeNull();
        giftCard!.Date.Should().Be(request.Date);
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        // Arrange
        var readOnlyClient = _factory.CreateClientWithClaims("GiftCardRead");

        // Act
        var response = await readOnlyClient.PostAsJsonAsync("/api/giftcards", new GiftCardBuilder().Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithExistingId_ReturnsOkAndUpdatesGiftCard()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/giftcards", new GiftCardBuilder().WithValue(200).Build());
        await using var db = _factory.CreateDbContext();
        var giftCard = await db.GiftCards.AsNoTracking().FirstAsync();

        var updateRequest = new GiftCardBuilder().WithValue(400).Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/giftcards/{giftCard.Id}", updateRequest);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database
        await using var db2 = _factory.CreateDbContext();
        var updated = await db2.GiftCards.AsNoTracking().SingleAsync(g => g.Id == giftCard.Id);
        updated.Value.Should().Be(400);
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.PutAsJsonAsync("/api/giftcards/nonexistent-9999", new GiftCardBuilder().Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsNoContentAndRemovesGiftCard()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/giftcards", new GiftCardBuilder().Build());
        await using var db = _factory.CreateDbContext();
        var giftCard = await db.GiftCards.AsNoTracking().FirstAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/giftcards/{giftCard.Id}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database: gift card removed
        await using var db2 = _factory.CreateDbContext();
        var inDb = await db2.GiftCards.AsNoTracking().SingleOrDefaultAsync(g => g.Id == giftCard.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync("/api/giftcards/nonexistent-9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
