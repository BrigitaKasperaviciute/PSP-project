using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class TimeSlotControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithAllClaims();
    private readonly HttpClient _anonClient = factory.CreateClient();

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/time-slot");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/time-slot");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingTimeSlot_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/time-slot/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistingTimeSlot_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/time-slot/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidTimeSlot_ReturnsOk()
    {
        var request = new
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(7),
            IsAvailable = true
        };

        var response = await _client.PostAsJsonAsync("/api/time-slot", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ExistingTimeSlot_ReturnsOk()
    {
        var createRequest = new
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(10),
            IsAvailable = true
        };
        var createResponse = await _client.PostAsJsonAsync("/api/time-slot", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeSlotResponse>();

        var updateRequest = new
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(11),
            IsAvailable = false
        };
        var response = await _client.PutAsJsonAsync($"/api/time-slot/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_ExistingTimeSlot_ReturnsOk()
    {
        var createRequest = new
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(14),
            IsAvailable = true
        };
        var createResponse = await _client.PostAsJsonAsync("/api/time-slot", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeSlotResponse>();

        var response = await _client.DeleteAsync($"/api/time-slot/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
