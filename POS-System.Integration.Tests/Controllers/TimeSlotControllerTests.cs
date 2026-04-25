using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

public class TimeSlotControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public TimeSlotControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Seeded time slots: Id=1,2 (EmployeeVersionId=1, available), Id=3 (EmployeeVersionId=2, unavailable), Id=4 (EmployeeVersionId=3, available)

    private async Task<TimeSlotResponse> CreateTimeSlotAsync(bool isAvailable = true)
    {
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddHours(1),
            IsAvailable = isAvailable
        };
        var response = await _authClient.PostAsJsonAsync("/api/time-slot", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TimeSlotResponse>())!;
    }

    [Fact]
    public async Task GetAll_ValidToken_ReturnsOkWithPagedResult()
    {
        // Arrange
        // (seeded time slots)

        // Act
        var response = await _authClient.GetAsync("/api/time-slot");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TimeSlotResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/time-slot");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_FilterByOnlyAvailable_ReturnsOnlyAvailableSlots()
    {
        // Arrange
        // (seeded slots include available and unavailable)

        // Act
        var response = await _authClient.GetAsync("/api/time-slot?onlyAvailable=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TimeSlotResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().AllSatisfy(ts => ts.IsAvailable.Should().BeTrue());
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithTimeSlot()
    {
        // Arrange
        const int existingId = 1; // seeded

        // Act
        var response = await _authClient.GetAsync($"/api/time-slot/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _authClient.GetAsync($"/api/time-slot/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithCreatedTimeSlot()
    {
        // Arrange
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(1),
            IsAvailable = true
        };

        // Act
        var response = await _authClient.PostAsJsonAsync("/api/time-slot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.IsAvailable.Should().BeTrue();
        body.EmployeeVersionId.Should().Be(1);
        body.Id.Should().BeGreaterThan(0);

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.TimeSlots.FindAsync(body.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddHours(2),
            IsAvailable = true
        };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/time-slot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ExistingId_ReturnsOkWithUpdatedTimeSlot()
    {
        // Arrange – create a dedicated time slot to update
        var created = await CreateTimeSlotAsync(true);
        var updateRequest = new TimeSlotRequest
        {
            EmployeeVersionId = 2,
            StartTime = DateTime.UtcNow.AddDays(2),
            IsAvailable = false
        };

        // Act
        var response = await _authClient.PutAsJsonAsync($"/api/time-slot/{created.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TimeSlotResponse>();
        body.Should().NotBeNull();
        body!.IsAvailable.Should().BeFalse();
        body.EmployeeVersionId.Should().Be(2);

        // Verify database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.TimeSlots.FindAsync(created.Id);
        saved!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsNotFound()
    {
        // Arrange — StartTime must be strictly greater than UtcNow (validator uses GreaterThan)
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddHours(1),
            IsAvailable = true
        };

        // Act
        var response = await _authClient.PutAsJsonAsync("/api/time-slot/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsOkAndRemovesSlot()
    {
        // Arrange – create a dedicated time slot to delete
        var created = await CreateTimeSlotAsync();

        // Act
        var response = await _authClient.DeleteAsync($"/api/time-slot/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify soft-deleted in database (IsAvailable=false, record remains)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await db.TimeSlots.FindAsync(created.Id);
        deleted.Should().NotBeNull();
        deleted!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        // (nothing to set up)

        // Act
        var response = await _authClient.DeleteAsync("/api/time-slot/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
