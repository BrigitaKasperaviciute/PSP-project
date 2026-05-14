using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection("Integration tests")]
public sealed class TimeSlotControllerTests : IntegrationTestBase
{
    public TimeSlotControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TimeSlotScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        (await client.GetAsync("/api/time-slot?pageNumber=0&pageSize=10")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/time-slot/1")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(1),
            IsAvailable = true
        };

        (await client.PostAsJsonAsync("/api/time-slot", createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        TimeSlot createdTimeSlot = await WithDbContextAsync(async context =>
            await context.TimeSlots.SingleAsync(timeSlot => timeSlot.EmployeeVersionId == createRequest.EmployeeVersionId && timeSlot.StartTime == createRequest.StartTime));

        var updateRequest = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(2),
            IsAvailable = false
        };

        (await client.PutAsJsonAsync($"/api/time-slot/{createdTimeSlot.Id}", updateRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/time-slot/{createdTimeSlot.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TimeSlotEndpoints_return_validation_and_not_found_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(-1),
            IsAvailable = true
        };

        (await client.PostAsJsonAsync("/api/time-slot", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/time-slot/99999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
