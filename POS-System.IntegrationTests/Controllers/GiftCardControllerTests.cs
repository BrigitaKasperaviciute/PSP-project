using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

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
    public async Task GetAll_WithGiftCardReadClaim_ReturnsOk()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/giftcards", new GiftCardBuilder().Build());

        // Act
        var response = await _client.GetAsync("/api/giftcards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedGiftCardResponse<GiftCardResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/giftcards");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsGiftCard()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/giftcards",
                new GiftCardBuilder().WithValue(10000).Build()))
            .Content.ReadFromJsonAsync<GiftCardResponse>();

        // Act
        var response = await _client.GetAsync($"/api/giftcards/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body!.Value.Should().Be(10000);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/giftcards/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersists()
    {
        // Arrange
        var request = new GiftCardBuilder().WithValue(7500).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/giftcards", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body!.Value.Should().Be(7500);

        // Assert - database (GiftCard entity uses a string PK; match by value)
        await using var db = _factory.CreateDbContext();
        var exists = await db.GiftCards.AsNoTracking().AnyAsync(g => g.Value == 7500);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        var readOnlyClient = _factory.CreateClientWithClaims("GiftCardRead");
        var response = await readOnlyClient.PostAsJsonAsync("/api/giftcards", new GiftCardBuilder().Build());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_ReturnsUpdatedGiftCard()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/giftcards", new GiftCardBuilder().WithValue(5000).Build()))
            .Content.ReadFromJsonAsync<GiftCardResponse>();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/giftcards/{created!.Id}",
            new GiftCardBuilder().WithValue(9999).Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body!.Value.Should().Be(9999);
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/giftcards/999999", new GiftCardBuilder().Build());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsNoContent()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/giftcards", new GiftCardBuilder().Build()))
            .Content.ReadFromJsonAsync<GiftCardResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/giftcards/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/giftcards/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record PagedGiftCardResponse<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
