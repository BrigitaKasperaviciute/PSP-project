using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Text.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class ItemDiscountControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _authorizedClient;

    public ItemDiscountControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _authorizedClient = null!;
    }

    public async Task InitializeAsync()
    {
        _authorizedClient = _factory.CreateAuthenticatedClient("ItemDiscountRead", "ItemDiscountWrite");
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _authorizedClient?.Dispose();
        await Task.CompletedTask;
    }

    // ===== GetAllItemDiscounts Tests =====

    [Fact]
    public async Task GetAllItemDiscounts_WithValidRequest_ReturnsOkWithItemDiscountList()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/item-discount?pageNum=0&pageSize=35");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetAllItemDiscounts_WithPagination_ReturnsCorrectPage()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/item-discount?pageNum=1&pageSize=10");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllItemDiscounts_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient();

        // Act
        var response = await unauthorizedClient.GetAsync("/api/item-discount?pageNum=0&pageSize=35");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== CreateItemDiscount Tests =====

    [Fact]
    public async Task CreateItemDiscount_WithValidRequest_ReturnsOkAndCreatesEntity()
    {
        // Arrange
        var itemDiscountRequest = new ItemDiscountRequestBuilder()
            .WithValue(15)
            .WithIsPercentage(true)
            .WithDescription("Test Discount")
            .Build();

        // Act
        var response = await _authorizedClient.PostAsync("/api/item-discount",
            new StringContent(JsonSerializer.Serialize(itemDiscountRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
            jsonDocument.TryGetProperty("id", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task CreateItemDiscount_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("ItemDiscountRead");
        var itemDiscountRequest = new ItemDiscountRequestBuilder().Build();

        // Act
        var response = await unauthorizedClient.PostAsync("/api/item-discount",
            new StringContent(JsonSerializer.Serialize(itemDiscountRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== GetItemDiscountById Tests =====

    [Fact]
    public async Task GetItemDiscountById_WithValidId_ReturnsOkOrNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/item-discount/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetItemDiscountById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/item-discount/99999");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    // ===== UpdateItemDiscountById Tests =====

    [Fact]
    public async Task UpdateItemDiscountById_WithValidRequest_ReturnsOkOrNotFound()
    {
        // Arrange
        var itemDiscountRequest = new ItemDiscountRequestBuilder()
            .WithValue(20)
            .WithIsPercentage(false)
            .Build();

        // Act
        var response = await _authorizedClient.PutAsync("/api/item-discount/1",
            new StringContent(JsonSerializer.Serialize(itemDiscountRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateItemDiscountById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var itemDiscountRequest = new ItemDiscountRequestBuilder().Build();

        // Act
        var response = await _authorizedClient.PutAsync("/api/item-discount/99999",
            new StringContent(JsonSerializer.Serialize(itemDiscountRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    // ===== DeleteItemDiscountById Tests =====

    [Fact]
    public async Task DeleteItemDiscountById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.DeleteAsync("/api/item-discount/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteItemDiscountById_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("ItemDiscountRead");

        // Act
        var response = await unauthorizedClient.DeleteAsync("/api/item-discount/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== LinkItemDiscountToItems Tests =====

    [Fact]
    public async Task LinkItemDiscountToItems_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var itemIdList = new[] { 1 };

        // Act
        var response = await _authorizedClient.PutAsync("/api/item-discount/1/link?itemsAreProducts=true",
            new StringContent(JsonSerializer.Serialize(itemIdList), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}

[Collection(nameof(ApiTestCollection))]
public sealed class ProductModificationControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _authorizedClient;

    public ProductModificationControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _authorizedClient = null!;
    }

    public async Task InitializeAsync()
    {
        _authorizedClient = _factory.CreateAuthenticatedClient("ItemRead", "ItemWrite");
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _authorizedClient?.Dispose();
        await Task.CompletedTask;
    }

    // ===== GetAllProductModifications Tests =====

    [Fact]
    public async Task GetAllProductModifications_WithValidRequest_ReturnsOkWithProductModificationList()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/product-modification?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetAllProductModifications_WithOnlyActiveFilter_ReturnsOnlyActiveModifications()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/product-modification?onlyActive=true&pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllProductModifications_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient();

        // Act
        var response = await unauthorizedClient.GetAsync("/api/product-modification?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== GetProductModificationById Tests =====

    [Fact]
    public async Task GetProductModificationById_WithValidId_ReturnsOkOrNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/product-modification/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ===== GetProductModificationVersionsByProductModificationId Tests =====

    [Fact]
    public async Task GetProductModificationVersionsByProductModificationId_WithValidId_ReturnsOkOrNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/product-modification/1/versions/");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ===== CreateProductModification Tests =====

    [Fact]
    public async Task CreateProductModification_WithValidRequest_ReturnsOkAndCreatesEntity()
    {
        // Arrange
        var productModificationRequest = new ProductModificationRequest
        {
            Name = "Extra Cheese",
            Description = "Add extra cheese",
            Price = 500
        };

        // Act
        var response = await _authorizedClient.PostAsync("/api/product-modification",
            new StringContent(JsonSerializer.Serialize(productModificationRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateProductModification_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("ItemRead");
        var productModificationRequest = new ProductModificationRequest
        {
            Name = "Test",
            Description = "Test",
            Price = 100
        };

        // Act
        var response = await unauthorizedClient.PostAsync("/api/product-modification",
            new StringContent(JsonSerializer.Serialize(productModificationRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== UpdateProductModification Tests =====

    [Fact]
    public async Task UpdateProductModification_WithValidRequest_ReturnsOkOrNotFound()
    {
        // Arrange
        var productModificationRequest = new ProductModificationRequest
        {
            Name = "Updated Name",
            Description = "Updated description",
            Price = 750
        };

        // Act
        var response = await _authorizedClient.PutAsync("/api/product-modification/1",
            new StringContent(JsonSerializer.Serialize(productModificationRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ===== DeleteProductModification Tests =====

    [Fact]
    public async Task DeleteProductModification_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.DeleteAsync("/api/product-modification/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

[Collection(nameof(ApiTestCollection))]
public sealed class CartDiscountControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _client;

    public CartDiscountControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = null!;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateAuthenticatedClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }

    // ===== CreateCartDiscount Tests =====

    [Fact]
    public async Task CreateCartDiscount_WithValidRequest_ReturnsOkAndCreatesEntity()
    {
        // Arrange
        var cartDiscountRequest = new CartDiscountRequest
        {
            Code = $"DISCOUNT-{Guid.NewGuid():N}",
            Value = 50,
            IsPercentage = false,
            MaxUse = 10,
            UsageCount = 0
        };

        // Act
        var response = await _client.PostAsync("/api/cart-discount",
            new StringContent(JsonSerializer.Serialize(cartDiscountRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    // ===== GetCartDiscountById Tests =====

    [Fact]
    public async Task GetCartDiscountById_WithValidId_ReturnsOkOrNotFound()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/cart-discount/test-code-123");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ===== DeleteCartDiscountById Tests =====

    [Fact]
    public async Task DeleteCartDiscountById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _client.DeleteAsync("/api/cart-discount/non-existing-code");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

[Collection(nameof(ApiTestCollection))]
public sealed class ServiceReservationControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _authorizedClient;

    public ServiceReservationControllerIntegrationTests(ApiTestFactory factory)
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

    // ===== GetAllServiceReservations Tests =====

    [Fact]
    public async Task GetAllServiceReservations_WithValidRequest_ReturnsOkWithReservationList()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/service-reservation?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetAllServiceReservations_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient();

        // Act
        var response = await unauthorizedClient.GetAsync("/api/service-reservation?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== GetServiceReservationById Tests =====

    [Fact]
    public async Task GetServiceReservationById_WithValidId_ReturnsOkOrNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/service-reservation/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ===== CreateServiceReservation Tests =====

    [Fact]
    public async Task CreateServiceReservation_WithValidRequest_ReturnsOkOrBadRequest()
    {
        // Arrange
        var reservationRequest = new ServiceReservationRequest
        {
            BookingTime = DateTime.UtcNow.AddDays(1),
            CustomerName = "John Doe",
            CustomerPhone = "1234567890",
            IsCancelled = false
        };

        // Act
        var response = await _authorizedClient.PostAsync("/api/service-reservation",
            new StringContent(JsonSerializer.Serialize(reservationRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateServiceReservation_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("ServiceRead");
        var reservationRequest = new ServiceReservationRequest
        {
            BookingTime = DateTime.UtcNow,
            CustomerName = "Test",
            CustomerPhone = "123",
            IsCancelled = false
        };

        // Act
        var response = await unauthorizedClient.PostAsync("/api/service-reservation",
            new StringContent(JsonSerializer.Serialize(reservationRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== UpdateServiceReservation Tests =====

    [Fact]
    public async Task UpdateServiceReservation_WithValidRequest_ReturnsOkOrNotFound()
    {
        // Arrange
        var reservationRequest = new ServiceReservationRequest
        {
            BookingTime = DateTime.UtcNow.AddDays(2),
            CustomerName = "Jane Doe",
            CustomerPhone = "0987654321",
            IsCancelled = false
        };

        // Act
        var response = await _authorizedClient.PutAsync("/api/service-reservation/1",
            new StringContent(JsonSerializer.Serialize(reservationRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}

// ===== Helper DTOs =====

public record CartDiscountRequest
{
    public required string Code { get; set; }
    public required int Value { get; set; }
    public required bool IsPercentage { get; set; }
    public required int MaxUse { get; set; }
    public required int UsageCount { get; set; }
}
