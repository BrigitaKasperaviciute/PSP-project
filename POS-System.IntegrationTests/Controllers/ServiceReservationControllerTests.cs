using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ServiceReservationControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public ServiceReservationControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateServiceReservation_WithValidPayload_ReturnsOkAndPersistsReservation()
    {
        // Arrange
        var request = new ServiceReservationRequestBuilder()
            .WithCartItemId(2)
            .WithTimeSlotId(1)
            .WithCustomerName("Alice Johnson")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/service-reservation", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.CustomerName.Should().Be("Alice Johnson");
        body.CartItemId.Should().Be(2);
        body.TimeSlotId.Should().Be(1);

        await using var db = _factory.CreateDbContext();
        var persisted = await db.ServiceReservations.AsNoTracking().FirstAsync(reservation => reservation.CustomerName == "Alice Johnson");
        persisted.CartItemId.Should().Be(2);
    }

    [Fact]
    public async Task GetServiceReservationById_WithExistingId_ReturnsOkAndReservation()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/service-reservation", new ServiceReservationRequestBuilder().WithCartItemId(2).WithTimeSlotId(1).Build());
        var createdReservation = await createResponse.Content.ReadFromJsonAsync<ServiceReservationResponse>();

        // Act
        var response = await _client.GetAsync($"/api/service-reservation/{createdReservation!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body!.Id.Should().Be(createdReservation.Id);
    }

    [Fact]
    public async Task UpdateServiceReservation_WithValidPayload_ReturnsOkAndUpdatesDatabase()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/service-reservation", new ServiceReservationRequestBuilder().WithCartItemId(2).WithTimeSlotId(1).Build());
        var createdReservation = await createResponse.Content.ReadFromJsonAsync<ServiceReservationResponse>();

        var updateRequest = new ServiceReservationRequestBuilder()
            .WithCartItemId(2)
            .WithTimeSlotId(1)
            .WithCustomerName("Updated Customer")
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/service-reservation/{createdReservation!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body!.CustomerName.Should().Be("Updated Customer");

        await using var db = _factory.CreateDbContext();
        var persisted = await db.ServiceReservations.AsNoTracking().FirstAsync(reservation => reservation.Id == createdReservation.Id);
        persisted.CustomerName.Should().Be("Updated Customer");
    }

    [Fact]
    public async Task GetServiceReservationById_WithMissingId_ReturnsNotFoundOrError()
    {
        // Act
        var response = await _client.GetAsync("/api/service-reservation/99999");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateServiceReservation_WithMissingCartItem_ReturnsBadRequestOrError()
    {
        // Arrange
        var request = new ServiceReservationRequestBuilder()
            .WithCartItemId(99999)
            .WithTimeSlotId(1)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/service-reservation", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }
}