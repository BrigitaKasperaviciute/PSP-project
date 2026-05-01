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
/// Seeded state:
///   CartItem Id=2  CartId=1  ServiceVersionId=1  IsProduct=false
///   TimeSlot Id=1  IsAvailable=true
///   No service reservations seeded — all are created by tests.
/// </summary>
public class ServiceReservationControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;
    private readonly HttpClient _writeClient;
    private readonly HttpClient _anonClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ServiceReservationControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _readClient  = factory.CreateClientWithClaims("ServiceRead");
        _writeClient = factory.CreateClientWithClaims("ServiceRead", "ServiceWrite");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Helper — creates a reservation against CartItem 2 (a service item in Cart 1)
    private async Task<ServiceReservationResponse> CreateReservationAsync(
        int cartItemId = 2, bool isCancelled = false)
    {
        var request = new ServiceReservationRequest
        {
            CartItemId     = cartItemId,
            TimeSlotId     = 1,
            BookingTime    = DateTime.UtcNow.AddDays(1),
            CustomerName   = "Test Customer",
            CustomerPhone  = "37060000099",
            IsCancelled    = isCancelled
        };
        var response = await _writeClient.PostAsJsonAsync("/api/service-reservation", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ServiceReservationResponse>(body, JsonOptions)!;
    }

    // ── GET /api/service-reservation ─────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithServiceReadClaim_ReturnsOkWithPagedResult()
    {
        // Arrange — create one reservation so the list is non-empty
        await CreateReservationAsync();

        // Act
        var response = await _readClient.GetAsync("/api/service-reservation");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<ServiceReservationResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/service-reservation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/service-reservation/{id} ────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithReservation()
    {
        // Arrange — create then retrieve
        var created = await CreateReservationAsync();

        // Act
        var response = await _readClient.GetAsync($"/api/service-reservation/{created.Id}");
        var body = await response.Content.ReadAsStringAsync();
        var reservation = JsonSerializer.Deserialize<ServiceReservationResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        reservation.Should().NotBeNull();
        reservation!.Id.Should().Be(created.Id);
        reservation.CustomerName.Should().Be("Test Customer");
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _readClient.GetAsync("/api/service-reservation/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/service-reservation ────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsOkAndPersistsReservation()
    {
        // Arrange — use CartItem 2 (a non-product item linked to Service 1)
        var bookingTime = DateTime.UtcNow.AddDays(2);
        var request = new ServiceReservationRequest
        {
            CartItemId    = 2,
            TimeSlotId    = 1,       // seeded, IsAvailable=true
            BookingTime   = bookingTime,
            CustomerName  = "Alice Jones",
            CustomerPhone = "37061111111",
            IsCancelled   = false
        };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/service-reservation", request);
        var body = await response.Content.ReadAsStringAsync();
        var reservation = JsonSerializer.Deserialize<ServiceReservationResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        reservation.Should().NotBeNull();
        reservation!.CustomerName.Should().Be("Alice Jones");
        reservation.CartItemId.Should().Be(2);

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.ServiceReservations.FindAsync(reservation.Id);
        persisted.Should().NotBeNull();
        persisted!.CustomerPhone.Should().Be("37061111111");
    }

    [Fact]
    public async Task Create_WithNonExistentCartItem_ReturnsError()
    {
        // Arrange — CartItem 9999 does not exist
        var request = new ServiceReservationRequest
        {
            CartItemId    = 9999,
            BookingTime   = DateTime.UtcNow.AddDays(1),
            CustomerName  = "Ghost",
            CustomerPhone = "00000000000",
            IsCancelled   = false
        };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/service-reservation", request);

        // Assert — service throws because the cart item FK doesn't exist
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    // ── PUT /api/service-reservation/{id} ────────────────────────────────────

    [Fact]
    public async Task Update_WithExistingId_ReturnsOkWithUpdatedReservation()
    {
        // Arrange
        var created = await CreateReservationAsync();
        var updateRequest = new ServiceReservationRequest
        {
            CartItemId    = 2,
            TimeSlotId    = null,
            BookingTime   = DateTime.UtcNow.AddDays(5),
            CustomerName  = "Updated Customer",
            CustomerPhone = "37062222222",
            IsCancelled   = true
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync(
            $"/api/service-reservation/{created.Id}", updateRequest);
        var body = await response.Content.ReadAsStringAsync();
        var reservation = JsonSerializer.Deserialize<ServiceReservationResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        reservation!.CustomerName.Should().Be("Updated Customer");
        reservation.IsCancelled.Should().BeTrue();

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.ServiceReservations.FindAsync(created.Id);
        persisted!.isCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ServiceReservationRequest
        {
            CartItemId    = 2,
            BookingTime   = DateTime.UtcNow.AddDays(1),
            CustomerName  = "Ghost",
            CustomerPhone = "00000000000",
            IsCancelled   = false
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/service-reservation/9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
