using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class ItemDiscountControllerIntegrationTests
{
    [Fact]
    public async Task CreateItemDiscount_ValidClaimAndPayload_PersistsDiscountAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["ItemDiscountWrite"]);

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.ItemDiscounts.CountAsync());
        var startDate = DateTime.UtcNow.AddMinutes(5);
        var payload = new
        {
            value = 20,
            isPercentage = true,
            description = "Integration discount",
            startDate,
            endDate = startDate.AddDays(7)
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/item-discount", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("value").GetInt32().Should().Be(20);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.ItemDiscounts.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateItemDiscount_MissingClaim_ReturnsForbiddenAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.ItemDiscounts.CountAsync());
        var payload = new
        {
            value = 20,
            isPercentage = true,
            description = "No claim discount",
            startDate = DateTime.UtcNow,
            endDate = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/item-discount", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.ItemDiscounts.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
