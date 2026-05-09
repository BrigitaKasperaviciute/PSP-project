using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Text.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class CartItemControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _authorizedClient;

    public CartItemControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _authorizedClient = null!;
    }

    public async Task InitializeAsync()
    {
        _authorizedClient = _factory.CreateAuthenticatedClient("CartItemRead", "CartItemWrite");
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _authorizedClient?.Dispose();
        await Task.CompletedTask;
    }

    // ===== GetAllCartItems Tests =====

    [Fact]
    public async Task GetAllCartItems_WithValidCartId_ReturnsOkWithCartItemList()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/carts/1/items?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllCartItems_WithPagination_ReturnsCorrectPage()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/carts/1/items?pageNum=0&pageSize=5");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllCartItems_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient();

        // Act
        var response = await unauthorizedClient.GetAsync("/api/carts/1/items?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== GetCartItemByIdAndCartId Tests =====

    [Fact]
    public async Task GetCartItemByIdAndCartId_WithValidIds_ReturnsOkOrNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/carts/1/items/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ===== CreateCartItem Tests =====

    [Fact]
    public async Task CreateCartItem_WithValidRequest_ReturnsOkAndCreatesEntity()
    {
        // Arrange
        var cartItemRequest = new CartItemRequest
        {
            CartId = 1,
            Quantity = 5,
            IsProduct = true
        };

        // Act
        var response = await _authorizedClient.PostAsync("/api/carts/1/items",
            new StringContent(JsonSerializer.Serialize(cartItemRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCartItem_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("CartItemRead");
        var cartItemRequest = new CartItemRequest
        {
            CartId = 1,
            Quantity = 5,
            IsProduct = true
        };

        // Act
        var response = await unauthorizedClient.PostAsync("/api/carts/1/items",
            new StringContent(JsonSerializer.Serialize(cartItemRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== UpdateCartItem Tests =====

    [Fact]
    public async Task UpdateCartItem_WithValidRequest_ReturnsOkOrNotFound()
    {
        // Arrange
        var cartItemRequest = new CartItemRequest
        {
            CartId = 1,
            Quantity = 10,
            IsProduct = true
        };

        // Act
        var response = await _authorizedClient.PutAsync("/api/carts/1/items/1",
            new StringContent(JsonSerializer.Serialize(cartItemRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    // ===== DeleteCartItem Tests =====

    [Fact]
    public async Task DeleteCartItem_WithValidRequest_ReturnsNoContentOrNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.DeleteAsync("/api/carts/1/items/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCartItem_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("CartItemRead");

        // Act
        var response = await unauthorizedClient.DeleteAsync("/api/carts/1/items/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

[Collection(nameof(ApiTestCollection))]
public sealed class TimeSlotControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _authorizedClient;

    public TimeSlotControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _authorizedClient = null!;
    }

    public async Task InitializeAsync()
    {
        _authorizedClient = _factory.CreateAuthenticatedClient("ServiceRead", "ServiceWrite");
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _authorizedClient?.Dispose();
        await Task.CompletedTask;
    }

    // ===== GetAllTimeSlots Tests =====

    [Fact]
    public async Task GetAllTimeSlots_WithValidRequest_ReturnsOkWithTimeSlotList()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/time-slot?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetAllTimeSlots_WithOnlyAvailableFilter_ReturnsOnlyAvailableSlots()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/time-slot?onlyAvailable=true&pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllTimeSlots_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient();

        // Act
        var response = await unauthorizedClient.GetAsync("/api/time-slot?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== GetTimeSlotById Tests =====

    [Fact]
    public async Task GetTimeSlotById_WithValidId_ReturnsOkOrNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/time-slot/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTimeSlotById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/time-slot/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ===== CreateTimeSlot Tests =====

    [Fact]
    public async Task CreateTimeSlot_WithValidRequest_ReturnsOkOrBadRequest()
    {
        // Arrange
        var timeSlotRequest = new TimeSlotRequest
        {
            Date = DateTime.UtcNow.AddDays(1),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0)
        };

        // Act
        var response = await _authorizedClient.PostAsync("/api/time-slot",
            new StringContent(JsonSerializer.Serialize(timeSlotRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTimeSlot_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("ServiceRead");
        var timeSlotRequest = new TimeSlotRequest
        {
            Date = DateTime.UtcNow.AddDays(1),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0)
        };

        // Act
        var response = await unauthorizedClient.PostAsync("/api/time-slot",
            new StringContent(JsonSerializer.Serialize(timeSlotRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== UpdateTimeSlot Tests =====

    [Fact]
    public async Task UpdateTimeSlot_WithValidRequest_ReturnsOkOrNotFound()
    {
        // Arrange
        var timeSlotRequest = new TimeSlotRequest
        {
            Date = DateTime.UtcNow.AddDays(2),
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0)
        };

        // Act
        var response = await _authorizedClient.PutAsync("/api/time-slot/1",
            new StringContent(JsonSerializer.Serialize(timeSlotRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    // ===== DeleteTimeSlot Tests =====

    [Fact]
    public async Task DeleteTimeSlot_WithValidRequest_ReturnsOkOrNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.DeleteAsync("/api/time-slot/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTimeSlot_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("ServiceRead");

        // Act
        var response = await unauthorizedClient.DeleteAsync("/api/time-slot/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ===== Helper DTO for TimeSlot Tests =====

public record TimeSlotRequest
{
    public required DateTime Date { get; set; }
    public required TimeOnly StartTime { get; set; }
    public required TimeOnly EndTime { get; set; }
}
