using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

public class PaymentControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _client;

    public PaymentControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        // PaymentController has no [Authorize] attributes
        _client = factory.CreateClient();
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<CartResponse> CreateFreshCartAsync()
    {
        // CartController has no auth; creates a cart with IN_PROGRESS status
        var request = new CartRequest { EmployeeVersionId = 1 };
        var response = await _client.PostAsJsonAsync("/api/carts", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CartResponse>())!;
    }

    private async Task<TransactionResponse> RegisterCashTransactionAsync(int cartId)
    {
        var request = new CashRequest(
            CartId: cartId,
            Amount: 1000,
            Tip: null,
            TransactionRef: Guid.NewGuid().ToString(),
            PhoneNumber: null
        );
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TransactionResponse>())!;
    }

    [Fact]
    public async Task RegisterCashTransaction_ValidRequest_ReturnsOkWithTransaction()
    {
        // Arrange
        var cart = await CreateFreshCartAsync();
        var request = new CashRequest(
            CartId: cart.Id,
            Amount: 2500,
            Tip: 100,
            TransactionRef: "TEST-REF-001",
            PhoneNumber: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
        body!.Amount.Should().Be(2500);
        body.TransactionRef.Should().Contain("TEST-REF-001");

        // Verify cart is now COMPLETED
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Carts.FindAsync(cart.Id);
        saved!.Status.Should().Be(Common.Enums.CartStatusEnum.COMPLETED);
    }

    [Fact]
    public async Task RegisterCashTransaction_NonExistentCart_ReturnsNotFound()
    {
        // Arrange
        var request = new CashRequest(
            CartId: 99999,
            Amount: 500,
            Tip: null,
            TransactionRef: "BAD-REF",
            PhoneNumber: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTransactionsByCart_CartWithTransactions_ReturnsOkWithList()
    {
        // Arrange – create a cart and register a cash payment to generate a transaction
        var cart = await CreateFreshCartAsync();
        await RegisterCashTransactionAsync(cart.Id);

        // Act
        var response = await _client.GetAsync($"/api/payments/{cart.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
        body![0].Amount.Should().Be(1000);
    }

    [Fact]
    public async Task GetTransactionsByCart_CartWithNoTransactions_ReturnsOkWithEmptyList()
    {
        // Arrange – create a fresh cart that has no payments yet
        var cart = await CreateFreshCartAsync();

        // Act
        var response = await _client.GetAsync($"/api/payments/{cart.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    [Fact]
    public async Task IssueRefund_CompletedCartCashTransaction_ReturnsOkWithRefund()
    {
        // Arrange – complete a cash transaction so we have something to refund
        var cart = await CreateFreshCartAsync();
        var transaction = await RegisterCashTransactionAsync(cart.Id);

        var refundRequest = new RefundRequest(CartId: cart.Id, IsCard: false);

        // Act – the route takes a DateTime as {id}; use ISO-8601 round-trip to preserve UTC kind
        var transactionDate = Uri.EscapeDataString(transaction.Id.ToString("o"));
        var response = await _client.PatchAsJsonAsync($"/api/payments/refund/{transactionDate}", refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(Common.Enums.TransactionStatusEnum.REFUNDED);
    }

    [Fact]
    public async Task IssueRefund_NonExistentCart_ReturnsNotFound()
    {
        // Arrange
        var refundRequest = new RefundRequest(CartId: 99999, IsCard: false);
        var transactionDate = Uri.EscapeDataString(DateTime.UtcNow.ToString("o"));

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/payments/refund/{transactionDate}", refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullCheckout_NonExistentCart_ReturnsNotFound()
    {
        // Arrange
        var request = new CheckoutRequest(
            CartId: 99999,
            EmployeeId: 1,
            Tip: null,
            PhoneNumber: null,
            CartItems: []
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/full-checkout", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullCheckout_CartNotInProgress_ReturnsBadRequest()
    {
        // Arrange – Cart Id=2 is seeded with COMPLETED status
        var request = new CheckoutRequest(
            CartId: 2,
            EmployeeId: 1,
            Tip: null,
            PhoneNumber: null,
            CartItems: []
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/full-checkout", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
