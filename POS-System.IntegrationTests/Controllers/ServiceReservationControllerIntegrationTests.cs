using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class ServiceReservationControllerIntegrationTests
{
    [Fact]
    public async Task GetAll_WithReadClaim_ReturnsPagedServiceReservations()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceRead" });

        // Arrange - create a reservation so the endpoint has stable data.
        await CreateReservationAsync(factory);

        // Act
        var response = await client.GetAsync("/api/service-reservation?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceReservationResponse?>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThan(0);
        body.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_WithReadClaim_ReturnsServiceReservation()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceRead" });

        var created = await CreateReservationAsync(factory);

        // Act
        var response = await client.GetAsync($"/api/service-reservation/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.CustomerName.Should().Be("Integration Customer");
    }

    [Fact]
    public async Task Create_WithWriteClaim_PersistsReservationAndMarksTimeSlotUnavailable()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceWrite" });

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.ServiceReservations.CountAsync());
        var payload = new ServiceReservationRequest
        {
            CartItemId = 2,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddDays(1),
            CustomerName = "Integration Customer",
            CustomerPhone = "+37060000002",
            IsCancelled = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/service-reservation", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.CartItemId.Should().Be(payload.CartItemId);
        body.TimeSlotId.Should().Be(payload.TimeSlotId);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.ServiceReservations.CountAsync());
        countAfter.Should().Be(countBefore + 1);

        var timeSlot = await factory.ExecuteDbContextAsync(db => db.TimeSlots.SingleAsync(slot => slot.Id == payload.TimeSlotId));
        timeSlot.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WithWriteClaim_UpdatesReservation()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceWrite" });

        var created = await CreateReservationAsync(factory);

        var payload = new ServiceReservationRequest
        {
            CartItemId = 1,
            TimeSlotId = 2,
            BookingTime = DateTime.UtcNow.AddDays(2),
            CustomerName = "Updated Customer",
            CustomerPhone = "+37060000003",
            IsCancelled = true
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/service-reservation/{created.Id}", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.CustomerName.Should().Be("Updated Customer");
        body.IsCancelled.Should().BeTrue();

        var persisted = await factory.ExecuteDbContextAsync(db => db.ServiceReservations.SingleAsync(reservation => reservation.Id == created.Id));
        persisted.CustomerPhone.Should().Be(payload.CustomerPhone);
        persisted.isCancelled.Should().BeTrue();
    }

    private static async Task<ServiceReservationResponse> CreateReservationAsync(ApiWebApplicationFactory factory)
    {
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceWrite" });

        var response = await client.PostAsJsonAsync("/api/service-reservation", new ServiceReservationRequest
        {
            CartItemId = 2,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddDays(1),
            CustomerName = "Integration Customer",
            CustomerPhone = "+37060000002",
            IsCancelled = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        return body!;
    }
}
