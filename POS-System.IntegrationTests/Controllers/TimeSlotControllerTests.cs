using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class TimeSlotControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TimeSlotControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ServiceReservations.RemoveRange(db.ServiceReservations.ToList());
        db.TimeSlots.RemoveRange(db.TimeSlots.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static TimeSlot MakeSlot(int id, bool available = true) => new()
    {
        Id = id,
        EmployeeVersionId = 1,
        StartTime = DateTime.UtcNow.AddDays(1),
        IsAvailable = available
    };

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllTimeSlots_WithValidAuth_ReturnsOkWithList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.TimeSlots.Add(MakeSlot(2001));
        db.TimeSlots.Add(MakeSlot(2002, available: false));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await client.GetAsync("/api/time-slot");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TimeSlotResponse>>(_jsonOptions);
        body!.TotalCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllTimeSlots_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/time-slot");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTimeSlotById_WithExistingId_ReturnsOkWithTimeSlot()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.TimeSlots.Add(MakeSlot(2003));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await client.GetAsync("/api/time-slot/2003");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>(_jsonOptions);
        body!.Id.Should().Be(2003);
        body.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetTimeSlotById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await client.GetAsync("/api/time-slot/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTimeSlot_WithValidRequest_ReturnsOkWithCreatedTimeSlot()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceWrite");
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(1),
            IsAvailable = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/time-slot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>(_jsonOptions);
        body!.Id.Should().BeGreaterThan(0);
        body.IsAvailable.Should().BeTrue();
        body.EmployeeVersionId.Should().Be(1);
    }

    [Fact]
    public async Task CreateTimeSlot_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new TimeSlotRequest { EmployeeVersionId = 1, StartTime = DateTime.UtcNow.AddDays(1), IsAvailable = true };

        // Act
        var response = await client.PostAsJsonAsync("/api/time-slot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTimeSlot_WithExistingId_ReturnsOkWithUpdatedTimeSlot()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.TimeSlots.Add(MakeSlot(2004));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceWrite");
        var request = new TimeSlotRequest { EmployeeVersionId = 2, StartTime = DateTime.UtcNow.AddDays(2), IsAvailable = false };

        // Act
        var response = await client.PutAsJsonAsync("/api/time-slot/2004", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>(_jsonOptions);
        body!.IsAvailable.Should().BeFalse();
        body.EmployeeVersionId.Should().Be(2);
    }

    [Fact]
    public async Task UpdateTimeSlot_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceWrite");
        var request = new TimeSlotRequest { EmployeeVersionId = 1, StartTime = DateTime.UtcNow.AddDays(1), IsAvailable = true };

        // Act
        var response = await client.PutAsJsonAsync("/api/time-slot/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTimeSlot_WithExistingId_ReturnsOkAndMarksUnavailable()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.TimeSlots.Add(MakeSlot(2005));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceWrite");

        // Act
        var response = await client.DeleteAsync("/api/time-slot/2005");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slot = await assertDb.TimeSlots.FindAsync(2005);
        slot!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTimeSlot_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceWrite");

        // Act
        var response = await client.DeleteAsync("/api/time-slot/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
