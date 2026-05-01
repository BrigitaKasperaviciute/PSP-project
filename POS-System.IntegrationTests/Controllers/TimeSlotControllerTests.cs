using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

/// <summary>
/// Seeded time slots:
///   Id=1  EmployeeVersionId=1  IsAvailable=true
///   Id=2  EmployeeVersionId=1  IsAvailable=true
///   Id=3  EmployeeVersionId=2  IsAvailable=false
///   Id=4  EmployeeVersionId=3  IsAvailable=true
/// </summary>
public class TimeSlotControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;
    private readonly HttpClient _writeClient;
    private readonly HttpClient _anonClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TimeSlotControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _readClient  = factory.CreateClientWithClaims("ServiceRead");
        _writeClient = factory.CreateClientWithClaims("ServiceRead", "ServiceWrite");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Helper
    private async Task<TimeSlotResponse> CreateTimeSlotAsync(bool isAvailable = true)
    {
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime         = DateTime.UtcNow.AddDays(7),
            IsAvailable       = isAvailable
        };
        var response = await _writeClient.PostAsJsonAsync("/api/time-slot", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TimeSlotResponse>(body, JsonOptions)!;
    }

    // ── GET /api/time-slot ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithServiceReadClaim_ReturnsOkWithPagedResult()
    {
        // Arrange — 4 time slots seeded

        // Act
        var response = await _readClient.GetAsync("/api/time-slot");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<TimeSlotResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task GetAll_WithOnlyAvailableFilter_ReturnsOnlyAvailableSlots()
    {
        // Arrange — seeded: 3 available, 1 not available

        // Act
        var response = await _readClient.GetAsync("/api/time-slot?onlyAvailable=true");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<TimeSlotResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged!.Results.Should().OnlyContain(ts => ts.IsAvailable);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/time-slot");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/time-slot/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithTimeSlot()
    {
        // Arrange — time slot Id=1 is seeded

        // Act
        var response = await _readClient.GetAsync("/api/time-slot/1");
        var body = await response.Content.ReadAsStringAsync();
        var slot = JsonSerializer.Deserialize<TimeSlotResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        slot.Should().NotBeNull();
        slot!.Id.Should().Be(1);
        slot.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _readClient.GetAsync("/api/time-slot/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/time-slot ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsOkAndPersistsTimeSlot()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddDays(3);
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 2,
            StartTime         = startTime,
            IsAvailable       = true
        };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/time-slot", request);
        var body = await response.Content.ReadAsStringAsync();
        var slot = JsonSerializer.Deserialize<TimeSlotResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        slot.Should().NotBeNull();
        slot!.IsAvailable.Should().BeTrue();
        slot.EmployeeVersionId.Should().Be(2);

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.TimeSlots.FindAsync(slot.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithoutServiceWriteClaim_ReturnsForbidden()
    {
        // Arrange — read-only client
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime         = DateTime.UtcNow.AddDays(1),
            IsAvailable       = true
        };

        // Act
        var response = await _readClient.PostAsJsonAsync("/api/time-slot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/time-slot/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task Update_WithExistingId_ReturnsOkWithUpdatedTimeSlot()
    {
        // Arrange — update slot Id=1 to not available
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime         = DateTime.UtcNow.AddDays(14),
            IsAvailable       = false
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/time-slot/1", request);
        var body = await response.Content.ReadAsStringAsync();
        var slot = JsonSerializer.Deserialize<TimeSlotResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        slot!.IsAvailable.Should().BeFalse();

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.TimeSlots.FindAsync(1);
        persisted!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1, StartTime = DateTime.UtcNow.AddHours(1), IsAvailable = true
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/time-slot/9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/time-slot/{id} ────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndRemovesTimeSlot()
    {
        // Arrange — create a slot then delete it (avoids modifying seeded data used by other tests)
        var created = await CreateTimeSlotAsync();

        // Act
        var response = await _writeClient.DeleteAsync($"/api/time-slot/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – time slot marked unavailable (soft-delete)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.TimeSlots.FindAsync(created.Id);
        persisted.Should().NotBeNull();
        persisted!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _writeClient.DeleteAsync("/api/time-slot/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
