using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ServiceReservationControllerTests
{
    [Fact]
    public async Task CreateUpdateAndReadServiceReservation_WorksEndToEnd()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ServiceRead,ServiceWrite");

        var createRequest = new ServiceReservationRequest
        {
            CartItemId = 1,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddHours(2),
            CustomerName = $"Customer {Guid.NewGuid():N}",
            CustomerPhone = "123456789",
            IsCancelled = false
        };

        var createResponse = await client.PostAsync("api/service-reservation", TestDataFactory.ToJsonContent(createRequest));
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK, createBody);
        createBody.Should().Contain(createRequest.CustomerName);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var createdReservation = db.ServiceReservations.Single(reservation => reservation.CustomerName == createRequest.CustomerName);
        db.TimeSlots.Single(slot => slot.Id == 1).IsAvailable.Should().BeFalse();

        var listResponse = await client.GetAsync("api/service-reservation?pageSize=10&pageNumber=0");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, listBody);
        listBody.Should().Contain(createRequest.CustomerName);

        var getResponse = await client.GetAsync($"api/service-reservation/{createdReservation.Id}");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, getBody);
        getBody.Should().Contain(createRequest.CustomerName);

        var updateRequest = new ServiceReservationRequest
        {
            CartItemId = 1,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddHours(3),
            CustomerName = $"{createRequest.CustomerName} updated",
            CustomerPhone = "987654321",
            IsCancelled = true
        };

        var updateResponse = await client.PutAsync($"api/service-reservation/{createdReservation.Id}", TestDataFactory.ToJsonContent(updateRequest));
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, updateBody);
        updateBody.Should().Contain(updateRequest.CustomerName);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        verificationDb.ServiceReservations.Single(reservation => reservation.Id == createdReservation.Id).CustomerName.Should().Be(updateRequest.CustomerName);
    }

    [Fact]
    public async Task CreateServiceReservation_NullBody_ReturnsBadRequest()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ServiceWrite");

        var response = await client.PostAsync("api/service-reservation", new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}