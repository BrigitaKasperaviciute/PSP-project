using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class TimeSlotControllerIntegrationTests
{
    [Fact]
    public async Task GetAllTimeSlots_WithReadClaim_ReturnsPagedTimeSlots()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceRead" });

        // Act
        var response = await client.GetAsync("/api/time-slot?pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TimeSlotResponse?>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThan(0);
        body.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_WithReadClaim_ReturnsTimeSlot()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceRead" });

        // Act
        var response = await client.GetAsync("/api/time-slot/4");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(4);
    }

    [Fact]
    public async Task CreateTimeSlot_ValidPayloadPersistsTimeSlotAndReturnsResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceWrite", "ServiceRead" });

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.TimeSlots.CountAsync());
        var payload = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(2),
            IsAvailable = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/time-slot", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdTimeSlot = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        createdTimeSlot.Should().NotBeNull();
        createdTimeSlot!.EmployeeVersionId.Should().Be(payload.EmployeeVersionId);
        createdTimeSlot.IsAvailable.Should().BeTrue();

        var countAfter = await factory.ExecuteDbContextAsync(db => db.TimeSlots.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task UpdateTimeSlot_ExistingTimeSlotPersistsChangesAndReturnsResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceWrite" });

        // Arrange
        var payload = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(3),
            IsAvailable = false
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/time-slot/1", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedTimeSlot = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        updatedTimeSlot.Should().NotBeNull();
        updatedTimeSlot!.IsAvailable.Should().BeFalse();

        var persistedTimeSlot = await factory.ExecuteDbContextAsync(db => db.TimeSlots.SingleAsync(x => x.Id == 1));
        persistedTimeSlot.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTimeSlot_ExistingTimeSlotReturnsOkAndRemovesRecord()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceWrite" });

        // Arrange

        // Act
        var response = await client.DeleteAsync("/api/time-slot/2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deletedTimeSlot = await factory.ExecuteDbContextAsync(db => db.TimeSlots.SingleAsync(x => x.Id == 2));
        deletedTimeSlot.IsAvailable.Should().BeFalse();
    }
}