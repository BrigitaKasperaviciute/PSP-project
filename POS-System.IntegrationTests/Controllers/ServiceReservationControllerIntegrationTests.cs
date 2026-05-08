using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class ServiceReservationControllerIntegrationTests
{
    [Fact]
    public async Task CreateServiceReservation_ValidClaimAndPayload_PersistsReservationAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["ServiceWrite"]);

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.ServiceReservations.CountAsync());
        var payload = new
        {
            cartItemId = 1,
            timeSlotId = 1,
            bookingTime = DateTime.UtcNow.AddDays(2),
            customerName = "Integration Customer",
            customerPhone = "+421900123123",
            isCancelled = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/service-reservation", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("customerName").GetString().Should().Be("Integration Customer");

        var countAfter = await factory.ExecuteDbContextAsync(db => db.ServiceReservations.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateServiceReservation_MissingClaim_ReturnsForbiddenAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.ServiceReservations.CountAsync());
        var payload = new
        {
            cartItemId = 1,
            timeSlotId = 1,
            bookingTime = DateTime.UtcNow.AddDays(1),
            customerName = "Blocked Customer",
            customerPhone = "+421900000000",
            isCancelled = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/service-reservation", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.ServiceReservations.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
