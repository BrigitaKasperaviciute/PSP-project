using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class TimeSlotControllerIntegrationTests
{
    [Fact]
    public async Task CreateTimeSlot_ValidClaimAndPayload_PersistsTimeSlotAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["ServiceWrite"]);

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.TimeSlots.CountAsync());
        var payload = new
        {
            employeeVersionId = 1,
            startTime = DateTime.UtcNow.AddDays(1),
            isAvailable = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/time-slot", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("employeeVersionId").GetInt32().Should().Be(1);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.TimeSlots.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateTimeSlot_MissingClaim_ReturnsForbiddenAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.TimeSlots.CountAsync());
        var payload = new
        {
            employeeVersionId = 1,
            startTime = DateTime.UtcNow.AddDays(3),
            isAvailable = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/time-slot", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.TimeSlots.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
