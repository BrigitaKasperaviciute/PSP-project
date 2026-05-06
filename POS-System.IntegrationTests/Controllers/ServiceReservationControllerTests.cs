using System.Net;
using Microsoft.AspNetCore.Http;
using Xunit;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ServiceReservationControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    // CartItem Id=2 in seeded data has ServiceVersionId=1 and is not a product
    // We'll use it for creating a reservation
    private const int ServiceCartItemId = 2;

    public ServiceReservationControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ServiceReservations.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetAllServiceReservations ---------------

    [Fact]
    public async Task GetAllServiceReservations_WhenReservationsExist_ReturnsOkWithPagedResults()
    {
        // Arrange
        await CreateReservationAsync(ServiceCartItemId, timeSlotId: 1);

        // Act
        var response = await _client.GetAsync("/api/service-reservation?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceReservationResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllServiceReservations_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/service-reservation?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- GetServiceReservationById ---------------

    [Fact]
    public async Task GetServiceReservationById_WhenReservationExists_ReturnsOkWithReservation()
    {
        // Arrange
        var created = await CreateReservationAsync(ServiceCartItemId, timeSlotId: 1);

        // Act
        var response = await _client.GetAsync($"/api/service-reservation/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.CartItemId.Should().Be(ServiceCartItemId);
        body.IsCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task GetServiceReservationById_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/service-reservation/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- CreateServiceReservation ---------------

    [Fact]
    public async Task CreateServiceReservation_WithValidRequest_ReturnsOkAndPersists()
    {
        // Arrange – seeded TimeSlot Id=2 is available; CartItem Id=2 has a service
        var request = new ServiceReservationRequest
        {
            CartItemId = ServiceCartItemId,
            TimeSlotId = 2,
            BookingTime = DateTime.UtcNow.AddDays(1),
            CustomerName = "Jane Smith",
            CustomerPhone = "+37061234567",
            IsCancelled = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/service-reservation", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.CartItemId.Should().Be(request.CartItemId);
        body.CustomerName.Should().Be(request.CustomerName);
        body.IsCancelled.Should().BeFalse();

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.ServiceReservations.AsNoTracking()
            .SingleOrDefaultAsync(sr => sr.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.CustomerPhone.Should().Be(request.CustomerPhone);
    }

    [Fact]
    public async Task CreateServiceReservation_WhenCartItemDoesNotExist_ReturnsInternalServerError()
    {
        // Arrange – CartItem 99999 does not exist; service inserts without checking FK → DbUpdateException → 500
        var request = new ServiceReservationRequest
        {
            CartItemId = 99999,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddDays(1),
            CustomerName = "Ghost User",
            CustomerPhone = "+37061111111",
            IsCancelled = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/service-reservation", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // --------------- UpdateServiceReservation ---------------

    [Fact]
    public async Task UpdateServiceReservation_WithValidRequest_ReturnsOkAndUpdates()
    {
        // Arrange
        var created = await CreateReservationAsync(ServiceCartItemId, timeSlotId: 1);
        var updateRequest = new ServiceReservationRequest
        {
            CartItemId = ServiceCartItemId,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddDays(5),
            CustomerName = "Updated Customer",
            CustomerPhone = "+37069999999",
            IsCancelled = true
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/service-reservation/{created.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.CustomerName.Should().Be(updateRequest.CustomerName);
        body.IsCancelled.Should().BeTrue();

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.ServiceReservations.AsNoTracking()
            .SingleOrDefaultAsync(sr => sr.Id == created.Id);
        persisted.Should().NotBeNull();
        persisted!.isCancelled.Should().BeTrue();
        persisted.CustomerName.Should().Be(updateRequest.CustomerName);
    }

    [Fact]
    public async Task UpdateServiceReservation_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var request = new ServiceReservationRequest
        {
            CartItemId = ServiceCartItemId,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow,
            CustomerName = "Ghost",
            CustomerPhone = "+37060000000",
            IsCancelled = false
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/service-reservation/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- helpers ----

    private async Task<ServiceReservationResponse> CreateReservationAsync(int cartItemId, int timeSlotId)
    {
        var response = await _client.PostAsJsonAsync("/api/service-reservation",
            new ServiceReservationRequest
            {
                CartItemId = cartItemId,
                TimeSlotId = timeSlotId,
                BookingTime = DateTime.UtcNow.AddDays(1),
                CustomerName = "Test Customer",
                CustomerPhone = "+37065555555",
                IsCancelled = false
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ServiceReservationResponse>())!;
    }
}
