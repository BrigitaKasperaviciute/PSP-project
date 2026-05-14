using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class GiftCardControllerIntegrationTests
{
    [Fact]
    public async Task GetAllGiftCards_WithReadClaim_ReturnsPagedGiftCards()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "GiftCardWrite", "GiftCardRead" });

        // Arrange
        await client.PostAsJsonAsync("/api/giftcards", new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            Value = 75
        });

        // Act
        var response = await client.GetAsync("/api/giftcards?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<GiftCardResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetGiftCardById_WithReadClaim_ReturnsGiftCard()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "GiftCardWrite", "GiftCardRead" });

        var createResponse = await client.PostAsJsonAsync("/api/giftcards", new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            Value = 125
        });
        var createdGiftCard = await createResponse.Content.ReadFromJsonAsync<GiftCardResponse>();

        // Act
        var response = await client.GetAsync($"/api/giftcards/{createdGiftCard!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdGiftCard.Id);
        body.Value.Should().Be(125);
    }

    [Fact]
    public async Task UpdateGiftCard_WithWriteClaim_UpdatesGiftCard()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "GiftCardWrite", "GiftCardRead" });

        var createResponse = await client.PostAsJsonAsync("/api/giftcards", new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            Value = 200
        });
        var createdGiftCard = await createResponse.Content.ReadFromJsonAsync<GiftCardResponse>();

        var updateRequest = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Value = 300
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/giftcards/{createdGiftCard!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(updateRequest.Value);
        body.Date.Should().Be(updateRequest.Date);
    }

    [Fact]
    public async Task CreateGiftCard_ValidPayloadPersistsGiftCardAndReturnsResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "GiftCardWrite", "GiftCardRead" });

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.GiftCards.CountAsync());
        var payload = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            Value = 100
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/giftcards", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdGiftCard = await response.Content.ReadFromJsonAsync<GiftCardResponse>();
        createdGiftCard.Should().NotBeNull();
        createdGiftCard!.Date.Should().Be(payload.Date);
        createdGiftCard.Value.Should().Be(payload.Value);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.GiftCards.CountAsync());
        countAfter.Should().Be(countBefore + 1);

        var getResponse = await client.GetAsync($"/api/giftcards/{createdGiftCard.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedGiftCard = await getResponse.Content.ReadFromJsonAsync<GiftCardResponse>();
        fetchedGiftCard.Should().NotBeNull();
        fetchedGiftCard!.Value.Should().Be(payload.Value);
    }

    [Fact]
    public async Task DeleteGiftCard_ExistingGiftCardRemovesRecordAndReturnsNoContent()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "GiftCardWrite" });

        // Arrange
        var createResponse = await client.PostAsJsonAsync("/api/giftcards", new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            Value = 250
        });
        var createdGiftCard = await createResponse.Content.ReadFromJsonAsync<GiftCardResponse>();
        createdGiftCard.Should().NotBeNull();

        // Act
        var deleteResponse = await client.DeleteAsync($"/api/giftcards/{createdGiftCard!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var exists = await factory.ExecuteDbContextAsync(db => db.GiftCards.AnyAsync(x => x.Value == 250 && x.Date == DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))));
        exists.Should().BeFalse();
    }
}