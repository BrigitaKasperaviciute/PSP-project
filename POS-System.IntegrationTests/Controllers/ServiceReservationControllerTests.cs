using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ServiceReservationControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ServiceReservationControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ServiceReservations.RemoveRange(db.ServiceReservations.ToList());
        db.TimeSlots.RemoveRange(db.TimeSlots.ToList());
        db.CartItems.RemoveRange(db.CartItems.ToList());
        db.Carts.RemoveRange(db.Carts.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static Cart MakeCart(int id) => new()
    {
        Id = id,
        EmployeeVersionId = 1,
        DateCreated = DateTime.UtcNow,
        IsDeleted = false,
        Status = CartStatusEnum.PENDING
    };

    private static CartItem MakeCartItem(int id, int cartId) => new()
    {
        Id = id,
        CartId = cartId,
        Quantity = 1,
        IsProduct = false,
        IsDeleted = false
    };

    private static TimeSlot MakeTimeSlot(int id) => new()
    {
        Id = id,
        EmployeeVersionId = 1,
        StartTime = DateTime.UtcNow.AddDays(1),
        IsAvailable = true
    };

    private static ServiceReservation MakeReservation(int id, int cartItemId, int? timeSlotId = null) => new()
    {
        Id = id,
        CartItemId = cartItemId,
        TimeSlotId = timeSlotId,
        BookingTime = DateTime.UtcNow,
        CustomerName = "John Doe",
        CustomerPhone = "1234567890",
        isCancelled = false
    };

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllServiceReservations_WithValidAuth_ReturnsOkWithList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(MakeCart(4001));
        db.CartItems.Add(MakeCartItem(4101, 4001));
        db.CartItems.Add(MakeCartItem(4102, 4001));
        db.ServiceReservations.Add(MakeReservation(4201, 4101));
        db.ServiceReservations.Add(MakeReservation(4202, 4102));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await client.GetAsync("/api/service-reservation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceReservationResponse>>(_jsonOptions);
        body!.TotalCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllServiceReservations_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/service-reservation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceReservationById_WithExistingId_ReturnsOkWithReservation()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(MakeCart(4002));
        db.CartItems.Add(MakeCartItem(4103, 4002));
        db.ServiceReservations.Add(MakeReservation(4203, 4103));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await client.GetAsync("/api/service-reservation/4203");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>(_jsonOptions);
        body!.Id.Should().Be(4203);
        body.CustomerName.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetServiceReservationById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await client.GetAsync("/api/service-reservation/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateServiceReservation_WithExistingTimeSlot_ReturnsOkWithCreatedReservation()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(MakeCart(4003));
        db.CartItems.Add(MakeCartItem(4104, 4003));
        db.TimeSlots.Add(MakeTimeSlot(4301));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceWrite");
        var request = new ServiceReservationRequest
        {
            CartItemId = 4104,
            TimeSlotId = 4301,
            BookingTime = DateTime.UtcNow,
            CustomerName = "Jane Smith",
            CustomerPhone = "0987654321",
            IsCancelled = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/service-reservation", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>(_jsonOptions);
        body!.Id.Should().BeGreaterThan(0);
        body.CustomerName.Should().Be("Jane Smith");

        // Verify TimeSlot is marked unavailable
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slot = await assertDb.TimeSlots.FindAsync(4301);
        slot!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CreateServiceReservation_WithNullTimeSlotId_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceWrite");
        var request = new ServiceReservationRequest
        {
            CartItemId = 1,
            TimeSlotId = null,
            BookingTime = DateTime.UtcNow,
            CustomerName = "No Slot",
            CustomerPhone = "1234567890",
            IsCancelled = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/service-reservation", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateServiceReservation_WithExistingId_ReturnsOkWithUpdatedReservation()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Carts.Add(MakeCart(4004));
        db.CartItems.Add(MakeCartItem(4105, 4004));
        db.ServiceReservations.Add(MakeReservation(4204, 4105));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceWrite");
        var request = new ServiceReservationRequest
        {
            CartItemId = 4105,
            TimeSlotId = null,
            BookingTime = DateTime.UtcNow.AddHours(2),
            CustomerName = "Updated Name",
            CustomerPhone = "5559991234",
            IsCancelled = true
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/service-reservation/4204", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>(_jsonOptions);
        body!.CustomerName.Should().Be("Updated Name");
        body.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateServiceReservation_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceWrite");
        var request = new ServiceReservationRequest
        {
            CartItemId = 1,
            TimeSlotId = null,
            BookingTime = DateTime.UtcNow,
            CustomerName = "Ghost",
            CustomerPhone = "1234567890",
            IsCancelled = false
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/service-reservation/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
