using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class CartDiscountControllerIntegrationTests
{
    [Fact]
    public async Task CreateCartDiscount_ValidPayload_PersistsDiscountAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.CardDiscounts.CountAsync());
        var payload = new
        {
            value = 15,
            isPercentage = true,
            endDate = DateTime.UtcNow.AddDays(14)
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/cart-discount", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("value").GetInt32().Should().Be(15);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.CardDiscounts.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateCartDiscount_InvalidJson_ReturnsBadRequestAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.CardDiscounts.CountAsync());

        // Act
        var response = await IntegrationTestHelpers.PostMalformedJsonAsync(client, "/api/cart-discount");

        // Assert
        await IntegrationTestHelpers.AssertBadRequestAsync(response);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.CardDiscounts.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
