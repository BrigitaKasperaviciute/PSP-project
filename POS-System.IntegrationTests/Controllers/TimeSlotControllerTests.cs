using System.Net;
using FluentAssertions;
using System;
using Microsoft.Extensions.DependencyInjection;
using POS_System.IntegrationTests.TestSupport;
using POS_System.IntegrationTests.Helpers;
using POS_System.Business.Dtos.Request;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class TimeSlotControllerTests
{
    [Fact]
    public async Task CreateTimeSlot_ValidRequest_ReturnsCreatedAndPersists()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ServiceWrite");

        var request = new TimeSlotRequest { EmployeeVersionId = 0, StartTime = DateTime.UtcNow.AddHours(1), IsAvailable = true };

        // Act
        var response = await client.PostAsync("api/time-slot", TestDataFactory.ToJsonContent(request));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.IndexOf("isAvailable", StringComparison.OrdinalIgnoreCase).Should().BeGreaterThan(-1);

        // Validate DB state
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var slot = db.TimeSlots.SingleOrDefault(s => s.EmployeeVersionId == 0);
        slot.Should().NotBeNull();
        slot!.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTimeSlot_NullBody_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ServiceWrite");

        // Act
        var response = await client.PostAsync("api/time-slot", new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
    }
}
