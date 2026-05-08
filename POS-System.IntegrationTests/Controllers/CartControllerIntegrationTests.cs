using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Common.Enums;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class CartControllerIntegrationTests
{
    [Fact]
    public async Task Create_ValidPayload_PersistsCartAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Carts.CountAsync());
        var payload = new { employeeVersionId = 1 };

        // Act
        var response = await client.PostAsJsonAsync("/api/carts", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("employeeVersionId").GetInt32().Should().Be(1);

        var createdCart = await factory.ExecuteDbContextAsync(db =>
            db.Carts
                .OrderByDescending(x => x.Id)
                .FirstAsync());

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Carts.CountAsync());
        countAfter.Should().Be(countBefore + 1);
        createdCart.Status.Should().Be(CartStatusEnum.IN_PROGRESS);
    }

    [Fact]
    public async Task Create_InvalidJson_ReturnsBadRequestAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Carts.CountAsync());

        // Act
        var response = await IntegrationTestHelpers.PostMalformedJsonAsync(client, "/api/carts");

        // Assert
        await IntegrationTestHelpers.AssertBadRequestAsync(response);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Carts.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
