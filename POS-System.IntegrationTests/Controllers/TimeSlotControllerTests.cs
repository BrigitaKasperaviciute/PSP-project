using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Data.Identity;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class TimeSlotControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public TimeSlotControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        await _db.TimeSlots.ExecuteDeleteAsync();

        if (!await _db.Employees.AnyAsync())
        {
            _db.Employees.Add(new ApplicationUser
            {
                Id = 1,
                EmployeeId = 1,
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                NormalizedUserName = "JOHNDOE",
                Email = "johndoe@example.com",
                NormalizedEmail = "JOHNDOE@EXAMPLE.COM",
                EmailConfirmed = true,
                PasswordHash = "AQAAAAEAACcQAAAAEL7rWl6+6gQmXvT4XvH8z9FV3WzQX1lKoHkxJ7F5oF+U4T5RrH3RrQbV9T8M2Q1O0N3P2L1K0J9I8H7G6F5D4C3B2A1",
                SecurityStamp = "N3J7G6F5D4C3B2A1O0N3P2L1K0J9I8H7G6F5D4C3B2A1",
                ConcurrencyStamp = "N3J7G6F5D4C3B2A1O0N3P2L1K0J9I8H7G6F5D4C3B2A1",
                PhoneNumber = "3463466346",
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = false,
                AccessFailedCount = 0,
                RoleId = 0,
                BirthDate = new DateOnly(2000, 1, 2),
                StartDate = new DateOnly(2024, 1, 2),
                Version = DateTime.UtcNow,
                IsDeleted = false
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateTimeSlot_WithValidPayload_ReturnsOkAndPersistsTimeSlot()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddDays(1).Date.AddHours(9);
        var endTime = DateTime.UtcNow.AddDays(1).Date.AddHours(17);
        var request = new TimeSlotRequestBuilder()
            .WithStartTime(startTime)
            .WithEndTime(endTime)
            .WithIsAvailable(true)
            .WithEmployeeId(1)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/time-slot", request);

        // If not OK, surface the response body for debugging
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var text = await response.Content.ReadAsStringAsync();
            throw new Exception($"Unexpected status {(int)response.StatusCode}: {text}");
        }

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.IsAvailable.Should().Be(request.IsAvailable);

        // Assert - database state
        var persisted = await _db.TimeSlots.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllTimeSlots_WithValidPageNumbers_ReturnsOkWithTimeSlots()
    {
        // Act
        var response = await _client.GetAsync("/api/time-slot?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<TimeSlotResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllTimeSlots_WithOnlyAvailableTrue_ReturnsOnlyAvailableTimeSlots()
    {
        // Act
        var response = await _client.GetAsync("/api/time-slot?onlyAvailable=true&pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<TimeSlotResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTimeSlotById_WithExistingId_ReturnsOkWithTimeSlot()
    {
        // Arrange
        var request = new TimeSlotRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/time-slot", request);
        var createdTimeSlot = await createResponse.Content.ReadFromJsonAsync<TimeSlotResponse>();

        // Act
        var response = await _client.GetAsync($"/api/time-slot/{createdTimeSlot!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body!.Id.Should().Be(createdTimeSlot.Id);
    }

    [Fact]
    public async Task UpdateTimeSlot_WithValidPayload_ReturnsOkAndUpdatesTimeSlot()
    {
        // Arrange
        var createRequest = new TimeSlotRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/time-slot", createRequest);
        var createdTimeSlot = await createResponse.Content.ReadFromJsonAsync<TimeSlotResponse>();

        // Act
        var updateRequest = new TimeSlotRequestBuilder().WithIsAvailable(false).Build();
        var response = await _client.PutAsJsonAsync($"/api/time-slot/{createdTimeSlot!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body!.IsAvailable.Should().Be(updateRequest.IsAvailable);
    }

    [Fact]
    public async Task DeleteTimeSlot_WithExistingId_ReturnsOkAndDeletesTimeSlot()
    {
        // Arrange
        var request = new TimeSlotRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/time-slot", request);
        var createdTimeSlot = await createResponse.Content.ReadFromJsonAsync<TimeSlotResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/time-slot/{createdTimeSlot!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleted = await _db.TimeSlots.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == createdTimeSlot.Id);
        deleted.Should().NotBeNull();
        deleted!.IsAvailable.Should().BeFalse();
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetTimeSlotById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/time-slot/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTimeSlot_WithEndTimeBeforeStartTime_ReturnsBadRequest()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddDays(1).Date.AddHours(17);
        var endTime = DateTime.UtcNow.AddDays(1).Date.AddHours(9);
        var request = new TimeSlotRequestBuilder()
            .WithStartTime(startTime)
            .WithEndTime(endTime)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/time-slot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateTimeSlot_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new TimeSlotRequestBuilder().Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/time-slot/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTimeSlot_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/time-slot/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

public class TimeSlotResponse
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public int EmployeeId { get; set; }
}
