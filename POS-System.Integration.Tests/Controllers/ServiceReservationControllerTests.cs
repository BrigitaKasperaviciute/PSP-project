using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

public class ServiceReservationControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public ServiceReservationControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<TimeSlotResponse> CreateFreshTimeSlotAsync()
    {
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(1),
            IsAvailable = true
        };
        var response = await _authClient.PostAsJsonAsync("/api/time-slot", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TimeSlotResponse>())!;
    }

    private async Task<ServiceReservationResponse> CreateReservationAsync()
    {
        var timeSlot = await CreateFreshTimeSlotAsync();
        var request = new ServiceReservationRequest
        {
            CartItemId = 2, // seeded CartItem with ServiceVersionId=1
            TimeSlotId = timeSlot.Id,
            BookingTime = DateTime.UtcNow.AddDays(1),
            CustomerName = "Test Customer",
            CustomerPhone = "555123456",
            IsCancelled = false
        };
        var response = await _authClient.PostAsJsonAsync("/api/service-reservation", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ServiceReservationResponse>())!;
    }

    [Fact]
    public async Task GetAll_ValidToken_ReturnsOkWithPagedResult()
    {
        // Arrange – ensure at least one reservation exists
        await CreateReservationAsync();

        // Act
        var response = await _authClient.GetAsync("/api/service-reservation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceReservationResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/service-reservation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithReservation()
    {
        // Arrange
        var created = await CreateReservationAsync();

        // Act
        var response = await _authClient.GetAsync($"/api/service-reservation/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.CustomerName.Should().Be("Test Customer");
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _authClient.GetAsync($"/api/service-reservation/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithCreatedReservation()
    {
        // Arrange – create a fresh time slot to avoid conflicts with other tests
        var timeSlot = await CreateFreshTimeSlotAsync();
        var request = new ServiceReservationRequest
        {
            CartItemId = 2,
            TimeSlotId = timeSlot.Id,
            BookingTime = DateTime.UtcNow.AddHours(3),
            CustomerName = "Jane Smith",
            CustomerPhone = "555987654",
            IsCancelled = false
        };

        // Act
        var response = await _authClient.PostAsJsonAsync("/api/service-reservation", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.CustomerName.Should().Be("Jane Smith");
        body.IsCancelled.Should().BeFalse();
        body.Id.Should().BeGreaterThan(0);

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.ServiceReservations.FindAsync(body.Id);
        saved.Should().NotBeNull();
        saved!.CustomerName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task Create_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ServiceReservationRequest
        {
            CartItemId = 2,
            TimeSlotId = 4,
            BookingTime = DateTime.UtcNow,
            CustomerName = "Anon",
            CustomerPhone = "000000000",
            IsCancelled = false
        };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/service-reservation", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ExistingId_ReturnsOkWithUpdatedReservation()
    {
        // Arrange – create a reservation to update
        var created = await CreateReservationAsync();
        var updateRequest = new ServiceReservationRequest
        {
            CartItemId = 2,
            TimeSlotId = created.TimeSlotId,
            BookingTime = DateTime.UtcNow.AddDays(3),
            CustomerName = "Updated Customer",
            CustomerPhone = "555000999",
            IsCancelled = true
        };

        // Act
        var response = await _authClient.PutAsJsonAsync($"/api/service-reservation/{created.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.CustomerName.Should().Be("Updated Customer");
        body.IsCancelled.Should().BeTrue();

        // Verify database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.ServiceReservations.FindAsync(created.Id);
        saved!.CustomerName.Should().Be("Updated Customer");
        saved.isCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ServiceReservationRequest
        {
            CartItemId = 2,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow,
            CustomerName = "Ghost",
            CustomerPhone = "000000000",
            IsCancelled = false
        };

        // Act
        var response = await _authClient.PutAsJsonAsync("/api/service-reservation/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
