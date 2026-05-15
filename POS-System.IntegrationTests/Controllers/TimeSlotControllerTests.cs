using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

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
        _client = factory.CreateClientWithClaims("ServiceRead", "ServiceWrite");
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ServiceReservations.ExecuteDeleteAsync();
        await db.TimeSlots.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithServiceReadClaim_ReturnsOk()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/time-slot", new TimeSlotBuilder().Build());

        // Act
        var response = await _client.GetAsync("/api/time-slot");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedTimeSlotResponse<TimeSlotResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/time-slot");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsTimeSlot()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/time-slot",
                new TimeSlotBuilder().WithEmployeeVersionId(1).WithIsAvailable(true).Build()))
            .Content.ReadFromJsonAsync<TimeSlotResponse>();

        // Act
        var response = await _client.GetAsync($"/api/time-slot/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body!.EmployeeVersionId.Should().Be(1);
        body.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/time-slot/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersists()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(3);
        var request = new TimeSlotBuilder().WithEmployeeVersionId(1).WithStartTime(startTime).WithIsAvailable(true).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/time-slot", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body!.EmployeeVersionId.Should().Be(1);
        body.IsAvailable.Should().BeTrue();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.TimeSlots.AsNoTracking().SingleOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        var readOnlyClient = _factory.CreateClientWithClaims("ServiceRead");
        var response = await readOnlyClient.PostAsJsonAsync("/api/time-slot", new TimeSlotBuilder().Build());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_ReturnsUpdatedTimeSlot()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/time-slot",
                new TimeSlotBuilder().WithIsAvailable(true).Build()))
            .Content.ReadFromJsonAsync<TimeSlotResponse>();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/time-slot/{created!.Id}",
            new TimeSlotBuilder().WithIsAvailable(false).Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/time-slot/999999", new TimeSlotBuilder().Build());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOk()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/time-slot", new TimeSlotBuilder().Build()))
            .Content.ReadFromJsonAsync<TimeSlotResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/time-slot/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - soft-deleted (IsAvailable = false), still in DB
        await using var db = _factory.CreateDbContext();
        var inDb = await db.TimeSlots.AsNoTracking().SingleOrDefaultAsync(t => t.Id == created.Id);
        inDb.Should().NotBeNull();
        inDb!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/time-slot/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record PagedTimeSlotResponse<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
