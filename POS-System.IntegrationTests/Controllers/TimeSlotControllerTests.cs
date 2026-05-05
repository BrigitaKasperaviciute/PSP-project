using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
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
    public async Task GetAll_WithValidAuth_ReturnsOkAndPagedTimeSlots()
    {
        // Arrange – employee ID 1 always exists (seeded)
        await _client.PostAsJsonAsync("/api/time-slot", new TimeSlotBuilder().WithEmployeeVersionId(1).Build());

        // Act
        var response = await _client.GetAsync("/api/time-slot?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<TimeSlotResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/time-slot");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectTimeSlot()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(2);
        var request = new TimeSlotBuilder().WithEmployeeVersionId(1).WithStartTime(startTime).WithIsAvailable(true).Build();
        var created = await (await _client.PostAsJsonAsync("/api/time-slot", request))
            .Content.ReadFromJsonAsync<TimeSlotResponse>();

        // Act
        var response = await _client.GetAsync($"/api/time-slot/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.IsAvailable.Should().BeTrue();
        body.EmployeeVersionId.Should().Be(1);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/time-slot/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsTimeSlot()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddDays(1);
        var request = new TimeSlotBuilder().WithEmployeeVersionId(1).WithStartTime(startTime).WithIsAvailable(true).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/time-slot", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.EmployeeVersionId.Should().Be(1);
        body.IsAvailable.Should().BeTrue();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.TimeSlots.AsNoTracking().SingleOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        // Arrange
        var readOnlyClient = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await readOnlyClient.PostAsJsonAsync("/api/time-slot", new TimeSlotBuilder().Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_ReturnsOkAndUpdatesTimeSlot()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/time-slot",
                new TimeSlotBuilder().WithEmployeeVersionId(1).WithIsAvailable(true).Build()))
            .Content.ReadFromJsonAsync<TimeSlotResponse>();

        var updateRequest = new TimeSlotBuilder().WithEmployeeVersionId(1).WithIsAvailable(false).Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/time-slot/{created!.Id}", updateRequest);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var updated = await db.TimeSlots.AsNoTracking().SingleOrDefaultAsync(t => t.Id == created.Id);
        updated!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.PutAsJsonAsync("/api/time-slot/999999", new TimeSlotBuilder().Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndRemovesTimeSlot()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/time-slot",
                new TimeSlotBuilder().WithEmployeeVersionId(1).Build()))
            .Content.ReadFromJsonAsync<TimeSlotResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/time-slot/{created!.Id}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var inDb = await db.TimeSlots.AsNoTracking().SingleOrDefaultAsync(t => t.Id == created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync("/api/time-slot/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
