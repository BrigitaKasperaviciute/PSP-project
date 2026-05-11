using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using POS_System.Data.Database;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class PaymentControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public PaymentControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        // Reset database for fresh state before each test
        // This ensures cart IDs 1-4 exist with proper initial status
        await ResetDatabaseAsync();
    }

    private async Task ResetDatabaseAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();
    }

    private async Task SetCartInProgressAsync(int cartId)
    {
        var cart = await _db.Carts.FindAsync(cartId);
        cart.Should().NotBeNull();
        cart!.Status = CartStatusEnum.IN_PROGRESS;
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Cash Transaction Tests

    [Fact]
    public async Task RegisterCashTransaction_WithValidPayload_ReturnsOk()
    {
        // Arrange
        var request = new CashRequestBuilder()
            .WithCartId(1)
            .WithAmount(10000)
            .WithTransactionRef("TXN-TEST-001")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterCashTransaction_WithTip_ReturnsOk()
    {
        // Arrange
        var request = new CashRequestBuilder()
            .WithAmount(15000)
            .WithTip(500)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterCashTransaction_WithPhoneNumber_ReturnsOk()
    {
        // Arrange
        var request = new CashRequestBuilder()
            .WithPhoneNumber("1234567890")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterCashTransaction_WithZeroAmount_ReturnsOk()
    {
        // Arrange
        var request = new CashRequestBuilder()
            .WithAmount(0)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterCashTransaction_WithLargeAmount_ReturnsOk()
    {
        // Arrange
        var request = new CashRequestBuilder()
            .WithAmount(1000000)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Get Transactions Tests

    [Fact]
    public async Task GetTransactionsByCart_WithValidCartId_ReturnsOk()
    {
        // Arrange
        var cartId = 1;
        var cashRequest = new CashRequestBuilder().WithCartId(cartId).Build();
        
        // Create a transaction first
        var createResponse = await _client.PostAsJsonAsync("/api/payments/cash", cashRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var response = await _client.GetAsync($"/api/payments/{cartId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTransactionsByCart_WithNonExistentCartId_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync($"/api/payments/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Refund Tests

    [Fact]
    public async Task IssueRefund_WithValidPayload_ReturnsOk()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        var refundRequest = new RefundRequest(CartId: 1, IsCard: true);

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/payments/refund/{transactionDate:O}", refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IssueRefund_WithCardPayment_ReturnsOk()
    {
        // Arrange
        var refundRequest = new RefundRequest(CartId: 1, IsCard: true);

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/payments/refund/{DateTime.UtcNow:O}", refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IssueRefund_WithCashPayment_ReturnsOk()
    {
        // Arrange
        var refundRequest = new RefundRequest(CartId: 1, IsCard: false);

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/payments/refund/{DateTime.UtcNow:O}", refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Checkout Tests

    [Fact]
    public async Task FullCheckout_WithValidPayload_ReturnsOk()
    {
        // Arrange
        await SetCartInProgressAsync(1);

        var request = new CheckoutRequest(
            CartId: 1,
            EmployeeId: 1,
            Tip: null,
            PhoneNumber: null,
            CartItems:
            [
                new CheckoutCartItem(
                    Name: "Test Item",
                    Description: "Integration checkout item",
                    Price: 1500,
                    Quantity: 1,
                    ImageURL: null)
            ]);

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/full-checkout", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        body.Should().NotBeNull();
        body!.SessionId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InitializePartialCheckout_WithValidPayload_ReturnsOkWithTransactions()
    {
        // Arrange
        await SetCartInProgressAsync(1);

        var request = new InitPartialCheckoutRequest(
            CartId: 1,
            EmployeeId: 1,
            PaymentCount: 2,
            Tip: null,
            CartItems:
            [
                new CheckoutCartItem(
                    Name: "Split Item",
                    Description: "Integration partial init",
                    Price: 4000,
                    Quantity: 1,
                    ImageURL: null)
            ]);

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/init-partial-checkout", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PartialCheckoutResponse>();
        body.Should().NotBeNull();
        body!.Transactions.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PartialCheckout_WithValidPayload_ReturnsOk()
    {
        // Arrange
        await SetCartInProgressAsync(1);

        var initRequest = new InitPartialCheckoutRequest(
            CartId: 1,
            EmployeeId: 1,
            PaymentCount: 2,
            Tip: null,
            CartItems:
            [
                new CheckoutCartItem(
                    Name: "Split Item",
                    Description: "Integration partial checkout",
                    Price: 4000,
                    Quantity: 1,
                    ImageURL: null)
            ]);

        var initResponse = await _client.PostAsJsonAsync("/api/payments/init-partial-checkout", initRequest);
        initResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initBody = await initResponse.Content.ReadFromJsonAsync<PartialCheckoutResponse>();
        initBody.Should().NotBeNull();
        var transactionId = initBody!.Transactions.First().Id;

        var request = new PartialCheckoutRequest(
            CartId: 1,
            Id: transactionId,
            GiftCard: null,
            PhoneNumber: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/partial-checkout", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        body.Should().NotBeNull();
        body!.SessionId.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region Redirect Endpoints Tests

    [Fact]
    public async Task FullCheckoutSuccess_WithValidParameters_ReturnsRedirect()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        var cartId = 1;
        var sessionId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync(
            $"/api/payments/full-checkout-success?transactionDate={transactionDate:O}&cartId={cartId}&sessionId={sessionId}");

        // Assert - Should return Redirect or OK
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.Found,
            HttpStatusCode.SeeOther,
            HttpStatusCode.TemporaryRedirect,
            HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task FullCheckoutSuccess_WithPhoneNumber_ReturnsRedirect()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        var sessionId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync(
            $"/api/payments/full-checkout-success?transactionDate={transactionDate:O}&cartId=1&sessionId={sessionId}&phoneNumber=1234567890");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.Found,
            HttpStatusCode.SeeOther,
            HttpStatusCode.TemporaryRedirect,
            HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task PartialCheckoutSuccess_WithValidParameters_ReturnsRedirect()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        var sessionId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync(
            $"/api/payments/partial-checkout-success?transactionDate={transactionDate:O}&cartId=1&sessionId={sessionId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.Found,
            HttpStatusCode.SeeOther,
            HttpStatusCode.TemporaryRedirect,
            HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task CheckoutFail_WithValidParameters_ReturnsRedirect()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        var sessionId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync(
            $"/api/payments/checkout-fail?transactionDate={transactionDate:O}&cartId=1&sessionId={sessionId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.Found,
            HttpStatusCode.SeeOther,
            HttpStatusCode.TemporaryRedirect,
            HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task CheckoutFail_WithGiftCardCode_ReturnsRedirect()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        var sessionId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync(
            $"/api/payments/checkout-fail?transactionDate={transactionDate:O}&cartId=1&sessionId={sessionId}&giftCardCode=GC-12345");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.Found,
            HttpStatusCode.SeeOther,
            HttpStatusCode.TemporaryRedirect,
            HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task CheckoutFail_WithDiscount_ReturnsRedirect()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        var sessionId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync(
            $"/api/payments/checkout-fail?transactionDate={transactionDate:O}&cartId=1&sessionId={sessionId}&discount=500");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.Found,
            HttpStatusCode.SeeOther,
            HttpStatusCode.TemporaryRedirect,
            HttpStatusCode.OK
        );
    }

    #endregion

    #region Multiple Operations Tests

    [Fact]
    public async Task RegisterMultipleCashTransactions_AllSucceed()
    {
        // Arrange
        var requests = new[]
        {
            new CashRequestBuilder().WithAmount(5000).Build(),
            new CashRequestBuilder().WithAmount(10000).Build(),
            new CashRequestBuilder().WithAmount(15000).Build()
        };

        // Act & Assert
        foreach (var request in requests)
        {
            var response = await _client.PostAsJsonAsync("/api/payments/cash", request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    #endregion
}
