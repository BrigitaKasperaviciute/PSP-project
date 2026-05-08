using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class GiftCardControllerIntegrationTests
{
    [Fact]
    public async Task CreateGiftCard_ValidClaimAndPayload_PersistsGiftCardAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["GiftCardWrite"]);

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.GiftCards.CountAsync());
        var payload = new
        {
            date = "2030-01-01",
            value = 125
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/giftcards", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("value").GetInt32().Should().Be(125);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.GiftCards.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateGiftCard_MissingClaim_ReturnsForbiddenAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.GiftCards.CountAsync());
        var payload = new
        {
            date = "2030-01-01",
            value = 80
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/giftcards", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.GiftCards.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
