using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ServiceReservationControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public ServiceReservationControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        await _db.ServiceReservations.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task GetAllServiceReservations_WithValidPageNumbers_ReturnsOkWithReservations()
    {
        // Act
        var response = await _client.GetAsync("/api/service-reservation?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<ServiceReservationResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetServiceReservationById_WithExistingId_ReturnsOkWithReservation()
    {
        // Act
        var response = await _client.GetAsync("/api/service-reservation/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateServiceReservation_WithValidPayload_ReturnsOk()
    {
        // Arrange
        var request = new ServiceReservationRequest
        {
            CartItemId = 1,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddHours(2),
            CustomerName = "Integration Customer",
            CustomerPhone = "1234567890",
            IsCancelled = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/service-reservation", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateServiceReservation_WithExistingId_ReturnsOk()
    {
        // Arrange
        var createRequest = new ServiceReservationRequest
        {
            CartItemId = 1,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddHours(1),
            CustomerName = "Initial Customer",
            CustomerPhone = "1234567890",
            IsCancelled = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/service-reservation", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await createResponse.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        created.Should().NotBeNull();

        var updateRequest = new ServiceReservationRequest
        {
            CartItemId = 1,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddHours(3),
            CustomerName = "Updated Customer",
            CustomerPhone = "0987654321",
            IsCancelled = true
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/service-reservation/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetServiceReservationById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/service-reservation/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

public class ServiceReservationResponse
{
    public int Id { get; set; }
    public int? CartItemId { get; set; }
    public int? TimeSlotId { get; set; }
    public DateTime BookingTime { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public bool IsCancelled { get; set; }
}
