using System.Net;
using Microsoft.AspNetCore.Http;
using Xunit;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class TimeSlotControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public TimeSlotControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    // Seeded time slots have Ids 1–4
    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.TimeSlots.Where(t => t.Id > 4).ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetAllTimeSlots ---------------

    [Fact]
    public async Task GetAllTimeSlots_WhenTimeSlotsExist_ReturnsOkWithPagedResults()
    {
        // Arrange – seeded time slots are present

        // Act
        var response = await _client.GetAsync("/api/time-slot?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TimeSlotResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllTimeSlots_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/time-slot?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- GetTimeSlotById ---------------

    [Fact]
    public async Task GetTimeSlotById_WhenTimeSlotExists_ReturnsOkWithTimeSlot()
    {
        // Arrange – seeded TimeSlot Id=1 (EmployeeVersionId=1, IsAvailable=true)
        const int existingId = 1;

        // Act
        var response = await _client.GetAsync($"/api/time-slot/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.EmployeeVersionId.Should().Be(1);
    }

    [Fact]
    public async Task GetTimeSlotById_WhenTimeSlotDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/time-slot/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- CreateTimeSlot ---------------

    [Fact]
    public async Task CreateTimeSlot_WithValidRequest_ReturnsOkAndPersistsTimeSlot()
    {
        // Arrange – seeded employee version Id=1
        var startTime = DateTime.UtcNow.AddDays(7);
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = startTime,
            IsAvailable = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/time-slot", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.EmployeeVersionId.Should().Be(request.EmployeeVersionId);
        body.IsAvailable.Should().BeTrue();

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.TimeSlots.AsNoTracking()
            .SingleOrDefaultAsync(ts => ts.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTimeSlot_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(1),
            IsAvailable = true
        };

        // Act
        var response = await unauthClient.PostAsJsonAsync("/api/time-slot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- UpdateTimeSlot ---------------

    [Fact]
    public async Task UpdateTimeSlot_WithValidRequest_ReturnsOkAndUpdates()
    {
        // Arrange
        var created = await CreateTimeSlotAsync(employeeVersionId: 1, isAvailable: true);
        var updateRequest = new TimeSlotRequest
        {
            EmployeeVersionId = 2,
            StartTime = DateTime.UtcNow.AddDays(14),
            IsAvailable = false
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/time-slot/{created.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.IsAvailable.Should().BeFalse();
        body.EmployeeVersionId.Should().Be(updateRequest.EmployeeVersionId);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.TimeSlots.AsNoTracking()
            .SingleOrDefaultAsync(ts => ts.Id == created.Id);
        persisted.Should().NotBeNull();
        persisted!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTimeSlot_WhenTimeSlotDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(1),
            IsAvailable = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/time-slot/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- DeleteTimeSlot ---------------

    [Fact]
    public async Task DeleteTimeSlot_WhenTimeSlotExists_ReturnsOkAndRemovesFromDb()
    {
        // Arrange
        var created = await CreateTimeSlotAsync(employeeVersionId: 1, isAvailable: true);

        // Act
        var response = await _client.DeleteAsync($"/api/time-slot/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – service sets IsAvailable=false (soft-delete), does not remove the row
        await using var db = _factory.CreateDbContext();
        var persisted = await db.TimeSlots.AsNoTracking()
            .SingleOrDefaultAsync(ts => ts.Id == created.Id);
        persisted.Should().NotBeNull();
        persisted!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTimeSlot_WhenTimeSlotDoesNotExist_ReturnsNotFound()
    {
        // Arrange

        // Act
        var response = await _client.DeleteAsync("/api/time-slot/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- helpers ----

    private async Task<TimeSlotResponse> CreateTimeSlotAsync(int employeeVersionId, bool isAvailable)
    {
        var response = await _client.PostAsJsonAsync("/api/time-slot",
            new TimeSlotRequest
            {
                EmployeeVersionId = employeeVersionId,
                StartTime = DateTime.UtcNow.AddDays(3),
                IsAvailable = isAvailable
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TimeSlotResponse>())!;
    }
}
