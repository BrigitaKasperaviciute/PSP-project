using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class TimeSlotControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public TimeSlotControllerTests(ApiLayerTestApplicationFactory factory)
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
        "{\"employeeVersionId\":1,\"startTime\":\"2030-06-01T10:00:00Z\",\"isAvailable\":true}";

    // --- GetAll ---

    [Fact]
    public async Task GetAll_DefaultParams_Returns200()
    {
        var response = await _client.GetAsync("/api/time-slot?pageSize=5&pageNumber=0");

        Assert.True(response.IsSuccessStatusCode);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_ExistingTimeSlot_Returns200()
    {
        var response = await _client.GetAsync("/api/time-slot/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetById_NonExistingTimeSlot_Returns404()
    {
        _factory.TimeSlotServiceMock
            .Setup(x => x.GetTimeSlotByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Time slot not found"));

        var response = await _client.GetAsync("/api/time-slot/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/time-slot", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsBadRequest_Returns400()
    {
        _factory.TimeSlotServiceMock
            .Setup(x => x.CreateTimeSlotAsync(It.IsAny<TimeSlotRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Employee not found"));

        var response = await _client.PostAsync("/api/time-slot", Json(ValidBody));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingTimeSlot_Returns200()
    {
        var response = await _client.PutAsync("/api/time-slot/1", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_NonExistingTimeSlot_Returns404()
    {
        _factory.TimeSlotServiceMock
            .Setup(x => x.UpdateTimeSlotAsync(It.IsAny<int>(), It.IsAny<TimeSlotRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Time slot not found"));

        var response = await _client.PutAsync("/api/time-slot/999", Json(ValidBody));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingTimeSlot_Returns200()
    {
        var response = await _client.DeleteAsync("/api/time-slot/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingTimeSlot_Returns404()
    {
        _factory.TimeSlotServiceMock
            .Setup(x => x.DeleteTimeSlotAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Time slot not found"));

        var response = await _client.DeleteAsync("/api/time-slot/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
