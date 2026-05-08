using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class BusinessDetailControllerIntegrationTests
{
    [Fact]
    public async Task GetBusinessDetails_ValidClaim_ReturnsOkWithBody()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["BusinessDetailsRead"]);

        // Arrange
        var seedEmployeeCount = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());

        // Act
        var response = await client.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Bakalaurui PSP");

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());
        countAfter.Should().Be(seedEmployeeCount);
    }

    [Fact]
    public async Task GetBusinessDetails_MissingClaim_ReturnsForbidden()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var seedEmployeeCount = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());

        // Act
        var response = await client.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());
        countAfter.Should().Be(seedEmployeeCount);
    }
}
