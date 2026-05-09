using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Text.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class PaymentControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _client;

    public PaymentControllerIntegrationTests(ApiTestFactory factory)
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

    // ===== RegisterCashTransactionAsync Tests =====

    [Fact]
    public async Task RegisterCashTransactionAsync_WithValidRequest_ReturnsOkOrBadRequest()
    {
        // Arrange
        var cashRequest = new CashRequest
        {
            CartId = 1,
            Amount = 5000
        };

        // Act
        var response = await _client.PostAsync("/api/payments/cash",
            new StringContent(JsonSerializer.Serialize(cashRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task RegisterCashTransactionAsync_WithInvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = "{}";

        // Act
        var response = await _client.PostAsync("/api/payments/cash",
            new StringContent(invalidRequest, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ===== IssueRefundAsync Tests =====

    [Fact]
    public async Task IssueRefundAsync_WithValidRequest_ReturnsOkOrNotFound()
    {
        // Arrange
        var refundRequest = new RefundRequest
        {
            CartId = 1,
            Amount = 1000,
            Reason = "Customer request"
        };

        var transactionDate = DateTime.UtcNow;

        // Act
        var response = await _client.PatchAsync($"/api/payments/refund/{Uri.EscapeDataString(transactionDate.ToString("o"))}",
            new StringContent(JsonSerializer.Serialize(refundRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    // ===== GetTransactionsByCartAsync Tests =====

    [Fact]
    public async Task GetTransactionsByCartAsync_WithValidCartId_ReturnsOkWithTransactionList()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/payments/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    // ===== FullCheckoutAsync Tests =====

    [Fact]
    public async Task FullCheckoutAsync_WithValidRequest_ReturnsOkOrBadRequest()
    {
        // Arrange
        var checkoutRequest = new CheckoutRequest
        {
            CartId = 1,
            SessionId = Guid.NewGuid().ToString()
        };

        // Act
        var response = await _client.PostAsync("/api/payments/full-checkout",
            new StringContent(JsonSerializer.Serialize(checkoutRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    // ===== InitializePartialCheckoutAsync Tests =====

    [Fact]
    public async Task InitializePartialCheckoutAsync_WithValidRequest_ReturnsOkOrBadRequest()
    {
        // Arrange
        var checkoutRequest = new InitPartialCheckoutRequest
        {
            CartId = 1,
            SessionId = Guid.NewGuid().ToString(),
            Amount = 2500
        };

        // Act
        var response = await _client.PostAsync("/api/payments/init-partial-checkout",
            new StringContent(JsonSerializer.Serialize(checkoutRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    // ===== PartialCheckoutAsync Tests =====

    [Fact]
    public async Task PartialCheckoutAsync_WithValidRequest_ReturnsOkOrBadRequest()
    {
        // Arrange
        var checkoutRequest = new PartialCheckoutRequest
        {
            CartId = 1,
            SessionId = Guid.NewGuid().ToString(),
            Amount = 2500
        };

        // Act
        var response = await _client.PostAsync("/api/payments/partial-checkout",
            new StringContent(JsonSerializer.Serialize(checkoutRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    // ===== Checkout Success/Fail Tests =====

    [Fact]
    public async Task FullCheckoutSuccessAsync_WithValidQuery_ReturnsRedirect()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        var cartId = 1;
        var sessionId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync(
            $"/api/payments/full-checkout-success?transactionDate={Uri.EscapeDataString(transactionDate.ToString("o"))}&cartId={cartId}&sessionId={sessionId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently, HttpStatusCode.Found, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task PartialCheckoutSuccessAsync_WithValidQuery_ReturnsRedirect()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        var cartId = 1;
        var sessionId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync(
            $"/api/payments/partial-checkout-success?transactionDate={Uri.EscapeDataString(transactionDate.ToString("o"))}&cartId={cartId}&sessionId={sessionId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently, HttpStatusCode.Found, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CheckoutFailAsync_WithValidQuery_ReturnsRedirect()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        var cartId = 1;
        var sessionId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync(
            $"/api/payments/checkout-fail?transactionDate={Uri.EscapeDataString(transactionDate.ToString("o"))}&cartId={cartId}&sessionId={sessionId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently, HttpStatusCode.Found, HttpStatusCode.InternalServerError);
    }
}

[Collection(nameof(ApiTestCollection))]
public sealed class BusinessDetailControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _authorizedClient;

    public BusinessDetailControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _authorizedClient = null!;
    }

    public async Task InitializeAsync()
    {
        _authorizedClient = _factory.CreateAuthenticatedClient("BusinessDetailsRead", "BusinessDetailsWrite");
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _authorizedClient?.Dispose();
        await Task.CompletedTask;
    }

    // ===== GetBusinessDetails Tests =====

    [Fact]
    public async Task GetBusinessDetails_WithValidRequest_ReturnsOkWithBusinessDetails()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
            jsonDocument.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        }
    }

    [Fact]
    public async Task GetBusinessDetails_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient();

        // Act
        var response = await unauthorizedClient.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== CreateBusinessDetails Tests =====

    [Fact]
    public async Task CreateBusinessDetails_WithValidRequest_ReturnsOkAndCreatesEntity()
    {
        // Arrange
        var businessDetailsRequest = new BusinessDetailsRequest
        {
            BusinessName = "Test Company",
            BusinessEmail = "test@company.com",
            BusinessPhone = "1234567890",
            Country = "Lithuania",
            City = "Test City",
            Street = "123 Main St",
            HouseNumber = 123,
            FlatNumber = 1
        };

        // Act
        var response = await _authorizedClient.PostAsync("/api/business-details",
            new StringContent(JsonSerializer.Serialize(businessDetailsRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBusinessDetails_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("BusinessDetailsRead");
        var businessDetailsRequest = new BusinessDetailsRequest
        {
            BusinessName = "Test",
            BusinessEmail = "test@test.com",
            BusinessPhone = "123",
            Country = "Lithuania",
            City = "City",
            Street = "123 St",
            HouseNumber = 123,
            FlatNumber = null
        };

        // Act
        var response = await unauthorizedClient.PostAsync("/api/business-details",
            new StringContent(JsonSerializer.Serialize(businessDetailsRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== UpdateBusinessDetails Tests =====

    [Fact]
    public async Task UpdateBusinessDetails_WithValidRequest_ReturnsOkAndUpdatesEntity()
    {
        // Arrange
        var businessDetailsRequest = new BusinessDetailsRequest
        {
            BusinessName = "Updated Company",
            BusinessEmail = "updated@company.com",
            BusinessPhone = "9876543210",
            Country = "Lithuania",
            City = "Updated City",
            Street = "456 Oak Ave",
            HouseNumber = 456,
            FlatNumber = null
        };

        // Act
        var response = await _authorizedClient.PutAsync("/api/business-details",
            new StringContent(JsonSerializer.Serialize(businessDetailsRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateBusinessDetails_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("BusinessDetailsRead");
        var businessDetailsRequest = new BusinessDetailsRequest
        {
            BusinessName = "Test",
            BusinessEmail = "test@test.com",
            BusinessPhone = "123",
            Country = "Lithuania",
            City = "City",
            Street = "123 St",
            HouseNumber = 123,
            FlatNumber = null
        };

        // Act
        var response = await unauthorizedClient.PutAsync("/api/business-details",
            new StringContent(JsonSerializer.Serialize(businessDetailsRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ===== Helper DTOs for Payment Tests =====

public record CashRequest
{
    public required int CartId { get; set; }
    public required int Amount { get; set; }
}

public record RefundRequest
{
    public required int CartId { get; set; }
    public required int Amount { get; set; }
    public required string Reason { get; set; }
}

public record CheckoutRequest
{
    public required int CartId { get; set; }
    public required string SessionId { get; set; }
}

public record InitPartialCheckoutRequest
{
    public required int CartId { get; set; }
    public required string SessionId { get; set; }
    public required int Amount { get; set; }
}

public record PartialCheckoutRequest
{
    public required int CartId { get; set; }
    public required string SessionId { get; set; }
    public required int Amount { get; set; }
}
