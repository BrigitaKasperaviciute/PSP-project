using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

public class GiftCardControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public GiftCardControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // No gift cards are seeded; every test that reads creates its own data first.

    private async Task<GiftCardResponse> CreateGiftCardAsync()
    {
        var request = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), // future date so it's valid
            Value = 500
        };
        var response = await _authClient.PostAsJsonAsync("/api/giftcards", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GiftCardResponse>())!;
    }

    [Fact]
    public async Task GetAll_ValidToken_ReturnsOkWithPagedResult()
    {
        // Arrange – ensure there is at least one gift card with a future date
        await CreateGiftCardAsync();

        // Act
        var response = await _authClient.GetAsync("/api/giftcards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<GiftCardResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token on _anonClient)

        // Act
        var response = await _anonClient.GetAsync("/api/giftcards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithGiftCard()
    {
        // Arrange
        var created = await CreateGiftCardAsync();

        // Act
        var response = await _authClient.GetAsync($"/api/giftcards/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(500);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const string nonExistentId = "00000000"; // will not match any generated ID

        // Act
        var response = await _authClient.GetAsync($"/api/giftcards/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithCreatedGiftCard()
    {
        // Arrange
        var request = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            Value = 1000
        };

        // Act
        var response = await _authClient.PostAsJsonAsync("/api/giftcards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(1000);
        body.Id.Should().BeGreaterThan(0);

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.GiftCards.FindAsync(body.Id.ToString());
        saved.Should().NotBeNull();
        saved!.Value.Should().Be(1000);
    }

    [Fact]
    public async Task Create_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Value = 100
        };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/giftcards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ExistingId_ReturnsOkWithUpdatedGiftCard()
    {
        // Arrange
        var created = await CreateGiftCardAsync();
        var updateRequest = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Value = 750
        };

        // Act
        var response = await _authClient.PutAsJsonAsync($"/api/giftcards/{created.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(750);

        // Verify database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.GiftCards.FindAsync(created.Id.ToString());
        saved!.Value.Should().Be(750);
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            Value = 100
        };

        // Act
        var response = await _authClient.PutAsJsonAsync("/api/giftcards/00000000", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsNoContentAndRemovesCard()
    {
        // Arrange
        var created = await CreateGiftCardAsync();

        // Act
        var response = await _authClient.DeleteAsync($"/api/giftcards/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await db.GiftCards.FindAsync(created.Id.ToString());
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        // (nothing to set up)

        // Act
        var response = await _authClient.DeleteAsync("/api/giftcards/00000000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
