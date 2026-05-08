using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ServiceReservationControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public ServiceReservationControllerTests(ApiLayerTestApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });
    }

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private const string ValidBody =
        "{\"cartItemId\":1,\"timeSlotId\":1,\"bookingTime\":\"2030-06-01T10:00:00Z\",\"customerName\":\"Alice\",\"customerPhone\":\"+37060000001\",\"isCancelled\":false}";

    // --- GetAll ---

    [Fact]
    public async Task GetAll_DefaultParams_Returns200()
    {
        var response = await _client.GetAsync("/api/service-reservation?pageSize=5&pageNumber=0");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetAll_ServiceThrowsInternalError_Returns500()
    {
        _factory.ServiceReservationServiceMock
            .Setup(x => x.GetServiceReservationsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var response = await _client.GetAsync("/api/service-reservation?pageSize=5&pageNumber=0");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/service-reservation", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsBadRequest_Returns400()
    {
        _factory.ServiceReservationServiceMock
            .Setup(x => x.CreateServiceReservationAsync(It.IsAny<ServiceReservationRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Time slot unavailable"));

        var response = await _client.PostAsync("/api/service-reservation", Json(ValidBody));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingReservation_Returns200()
    {
        var response = await _client.PutAsync("/api/service-reservation/1", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_NonExistingReservation_Returns404()
    {
        _factory.ServiceReservationServiceMock
            .Setup(x => x.UpdateServiceReservationByIdAsync(It.IsAny<int>(), It.IsAny<ServiceReservationRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Service reservation not found"));

        var response = await _client.PutAsync("/api/service-reservation/999", Json(ValidBody));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
