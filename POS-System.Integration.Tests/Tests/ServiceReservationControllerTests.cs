using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class ServiceReservationControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithAllClaims();
    private readonly HttpClient _anonClient = factory.CreateClient();

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/service-reservation");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/service-reservation");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_NonExistingReservation_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/service-reservation/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ExistingReservation_ReturnsOk()
    {
        var cartItemRequest = new
        {
            CartId = 3,
            Quantity = 1,
            IsProduct = false,
            ServiceVersionId = 1
        };
        var cartItemResponse = await _client.PostAsJsonAsync("/api/carts/3/items", cartItemRequest);
        var cartItem = await cartItemResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        var createRequest = new
        {
            CartItemId = cartItem!.Id,
            TimeSlotId = 4,
            BookingTime = DateTime.UtcNow.AddDays(2),
            CustomerName = "Test Customer",
            CustomerPhone = "111222333",
            IsCancelled = false
        };
        var createResponse = await _client.PostAsJsonAsync("/api/service-reservation", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceReservationResponse>();

        var response = await _client.GetAsync($"/api/service-reservation/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_ValidReservation_ReturnsOk()
    {
        var cartItemRequest = new
        {
            CartId = 3,
            Quantity = 1,
            IsProduct = false,
            ServiceVersionId = 1
        };
        var cartItemResponse = await _client.PostAsJsonAsync("/api/carts/3/items", cartItemRequest);
        var cartItem = await cartItemResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        var request = new
        {
            CartItemId = cartItem!.Id,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddDays(3),
            CustomerName = "John Doe",
            CustomerPhone = "123456789",
            IsCancelled = false
        };

        var response = await _client.PostAsJsonAsync("/api/service-reservation", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ExistingReservation_ReturnsOk()
    {
        var cartItemRequest = new
        {
            CartId = 3,
            Quantity = 1,
            IsProduct = false,
            ServiceVersionId = 1
        };
        var cartItemResponse = await _client.PostAsJsonAsync("/api/carts/3/items", cartItemRequest);
        var cartItem = await cartItemResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        var createRequest = new
        {
            CartItemId = cartItem!.Id,
            TimeSlotId = 2,
            BookingTime = DateTime.UtcNow.AddDays(5),
            CustomerName = "Jane Smith",
            CustomerPhone = "987654321",
            IsCancelled = false
        };
        var createResponse = await _client.PostAsJsonAsync("/api/service-reservation", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceReservationResponse>();

        var updateRequest = new
        {
            CartItemId = cartItem.Id,
            TimeSlotId = (int?)null,
            BookingTime = DateTime.UtcNow.AddDays(6),
            CustomerName = "Jane Smith Updated",
            CustomerPhone = "987654321",
            IsCancelled = true
        };
        var response = await _client.PutAsJsonAsync($"/api/service-reservation/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
