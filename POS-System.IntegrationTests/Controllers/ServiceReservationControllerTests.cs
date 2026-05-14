using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection("Integration tests")]
public sealed class ServiceReservationControllerTests : IntegrationTestBase
{
    public ServiceReservationControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ServiceReservationScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        (await client.GetAsync("/api/service-reservation?pageNumber=0&pageSize=10")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new ServiceReservationRequest
        {
            CartItemId = 2,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddDays(1),
            CustomerName = "Reservation Customer",
            CustomerPhone = "12345678901",
            IsCancelled = false
        };

        (await client.PostAsJsonAsync("/api/service-reservation", createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        ServiceReservation createdReservation = await WithDbContextAsync(async context =>
            await context.ServiceReservations.SingleAsync(reservation => reservation.CartItemId == createRequest.CartItemId && reservation.CustomerName == createRequest.CustomerName));

        var updateRequest = new ServiceReservationRequest
        {
            CartItemId = 2,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddDays(2),
            CustomerName = "Reservation Customer Updated",
            CustomerPhone = "12345678902",
            IsCancelled = true
        };

        (await client.PutAsJsonAsync($"/api/service-reservation/{createdReservation.Id}", updateRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/service-reservation/{createdReservation.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ServiceReservationEndpoints_return_validation_and_not_found_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new ServiceReservationRequest
        {
            CartItemId = 2,
            TimeSlotId = null,
            BookingTime = DateTime.UtcNow.AddDays(1),
            CustomerName = "Reservation Customer",
            CustomerPhone = "12345678901",
            IsCancelled = false
        };

        (await client.PostAsJsonAsync("/api/service-reservation", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/service-reservation/99999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
