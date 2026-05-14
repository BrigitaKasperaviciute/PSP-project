using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Business.Dtos;
using POS_System.Common.Enums;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class TimeSlotControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public TimeSlotControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient(role: "Admin");
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateTimeSlot_WithValidPayload_ReturnsOkAndPersistsTimeSlot()
    {
        // Arrange
        var employee = await CreateEmployeeAsync();
        var request = new TimeSlotRequestBuilder()
            .WithEmployeeVersionId(employee.Id)
            .WithStartTime(DateTime.UtcNow.AddHours(2))
            .WithIsAvailable(true)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/time-slot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.EmployeeVersionId.Should().Be(employee.Id);
        body.IsAvailable.Should().BeTrue();
        body.StartTime.Should().BeCloseTo(request.StartTime, TimeSpan.FromSeconds(1));

        await using var db = _factory.CreateDbContext();
        var persisted = await db.TimeSlots.AsNoTracking().SingleAsync(timeSlot => timeSlot.Id == body.Id);
        persisted.EmployeeVersionId.Should().Be(employee.Id);
        persisted.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllTimeSlots_WithSeededRows_ReturnsOkAndPagedResults()
    {
        // Arrange
        var employee = await CreateEmployeeAsync();
        await CreateTimeSlotAsync(employee.Id, DateTime.UtcNow.AddHours(1));
        await CreateTimeSlotAsync(employee.Id, DateTime.UtcNow.AddHours(2));

        // Act
        var response = await _client.GetAsync("/api/time-slot?onlyAvailable=true&pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TimeSlotResponse?>>();
        body.Should().NotBeNull();
        body!.Results.Should().HaveCountGreaterOrEqualTo(2);
        body.TotalCount.Should().BeGreaterOrEqualTo(2);

        body.Results.Count(result => result is not null && result!.EmployeeVersionId == employee.Id)
            .Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetTimeSlotById_WithExistingId_ReturnsOkAndTimeSlot()
    {
        // Arrange
        var employee = await CreateEmployeeAsync();
        var createdTimeSlot = await CreateTimeSlotAsync(employee.Id, DateTime.UtcNow.AddHours(3));

        // Act
        var response = await _client.GetAsync($"/api/time-slot/{createdTimeSlot.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdTimeSlot.Id);
        body.EmployeeVersionId.Should().Be(employee.Id);
    }

    [Fact]
    public async Task UpdateTimeSlot_WithValidPayload_ReturnsOkAndUpdatesDatabase()
    {
        // Arrange
        var employee = await CreateEmployeeAsync();
        var createdTimeSlot = await CreateTimeSlotAsync(employee.Id, DateTime.UtcNow.AddHours(4));
        var updateEmployee = await CreateEmployeeAsync();
        var updateRequest = new TimeSlotRequestBuilder()
            .WithEmployeeVersionId(updateEmployee.Id)
            .WithStartTime(DateTime.UtcNow.AddHours(5))
            .WithIsAvailable(false)
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/time-slot/{createdTimeSlot.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.EmployeeVersionId.Should().Be(updateEmployee.Id);
        body.IsAvailable.Should().BeFalse();

        await using var db = _factory.CreateDbContext();
        var persisted = await db.TimeSlots.AsNoTracking().SingleAsync(timeSlot => timeSlot.Id == createdTimeSlot.Id);
        persisted.EmployeeVersionId.Should().Be(updateEmployee.Id);
        persisted.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTimeSlot_WithExistingId_ReturnsOkAndMarksTimeSlotUnavailable()
    {
        // Arrange
        var employee = await CreateEmployeeAsync();
        var createdTimeSlot = await CreateTimeSlotAsync(employee.Id, DateTime.UtcNow.AddHours(6));

        // Act
        var response = await _client.DeleteAsync($"/api/time-slot/{createdTimeSlot.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.IsAvailable.Should().BeFalse();

        await using var db = _factory.CreateDbContext();
        var persisted = await db.TimeSlots.AsNoTracking().SingleAsync(timeSlot => timeSlot.Id == createdTimeSlot.Id);
        persisted.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTimeSlot_WithMissingStartTime_ReturnsBadRequest()
    {
        // Arrange
        var employee = await CreateEmployeeAsync();
        var request = new { EmployeeVersionId = employee.Id, IsAvailable = true };

        // Act
        var response = await _client.PostAsJsonAsync("/api/time-slot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTimeSlotById_WithMissingId_ReturnsNotFound()
    {
        // Arrange
        const int missingId = 999999;

        // Act
        var response = await _client.GetAsync($"/api/time-slot/{missingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<EmployeeResponse> CreateEmployeeAsync()
    {
        var request = new UserRegisterRequest(
            Email: $"employee-{Guid.NewGuid():N}@example.com",
            UserName: $"employee-{Guid.NewGuid():N}",
            FirstName: "Test",
            LastName: "Employee",
            Password: "Test@1234!",
            PhoneNumber: "+1234567890",
            BirthDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25)),
            RoleId: 1
        );

        var response = await _client.PostAsJsonAsync("/api/employees/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        return body!;
    }

    private async Task<TimeSlotResponse> CreateTimeSlotAsync(int employeeVersionId, DateTime startTime)
    {
        var request = new TimeSlotRequestBuilder()
            .WithEmployeeVersionId(employeeVersionId)
            .WithStartTime(startTime)
            .WithIsAvailable(true)
            .Build();

        var response = await _client.PostAsJsonAsync("/api/time-slot", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        return body!;
    }
}
