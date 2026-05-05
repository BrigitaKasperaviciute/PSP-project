using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class PaymentControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _noRedirectClient;

    public PaymentControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _noRedirectClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    // --- RegisterCashTransaction ---

    [Fact]
    public async Task RegisterCashTransaction_WithValidRequest_ReturnsOk()
    {
        var request = new CashRequest(CartId: 1, Amount: 1000UL, Tip: null, TransactionRef: "ref-001", PhoneNumber: null);

        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(TransactionStatusEnum.CASH);
    }

    // --- GetTransactionsByCart ---

    [Fact]
    public async Task GetTransactionsByCart_WithCartId_ReturnsOkAndList()
    {
        var response = await _client.GetAsync("/api/payments/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
    }

    // --- IssueRefund ---

    [Fact]
    public async Task IssueRefund_WithValidRequest_ReturnsOk()
    {
        var transactionDate = DateTime.UtcNow;
        var request = new RefundRequest(CartId: 1, IsCard: false);

        var response = await _client.PatchAsJsonAsync($"/api/payments/refund/{transactionDate:o}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(TransactionStatusEnum.REFUNDED);
    }

    // --- FullCheckout ---

    [Fact]
    public async Task FullCheckout_WithValidRequest_ReturnsOk()
    {
        var request = new CheckoutRequest(
            CartId: 1,
            EmployeeId: 1,
            Tip: null,
            PhoneNumber: null,
            CartItems: new List<CheckoutCartItem>()
        );

        var response = await _client.PostAsJsonAsync("/api/payments/full-checkout", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        body.Should().NotBeNull();
        body!.SessionId.Should().Be("fake-session-id");
    }

    // --- InitPartialCheckout ---

    [Fact]
    public async Task InitPartialCheckout_WithValidRequest_ReturnsOk()
    {
        var request = new InitPartialCheckoutRequest(
            CartId: 1,
            EmployeeId: 1,
            PaymentCount: 2,
            Tip: null,
            CartItems: new List<CheckoutCartItem>()
        );

        var response = await _client.PostAsJsonAsync("/api/payments/init-partial-checkout", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PartialCheckoutResponse>();
        body.Should().NotBeNull();
        body!.Transactions.Should().NotBeEmpty();
    }

    // --- PartialCheckout ---

    [Fact]
    public async Task PartialCheckout_WithValidRequest_ReturnsOk()
    {
        var request = new PartialCheckoutRequest(
            CartId: 1,
            Id: DateTime.UtcNow,
            GiftCard: null,
            PhoneNumber: null
        );

        var response = await _client.PostAsJsonAsync("/api/payments/partial-checkout", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        body.Should().NotBeNull();
        body!.SessionId.Should().Be("fake-session-id");
    }

    // --- FullCheckoutSuccess ---

    [Fact]
    public async Task FullCheckoutSuccess_ReturnsRedirectToSuccessUrl()
    {
        var date = DateTime.UtcNow;
        var url = $"/api/payments/full-checkout-success?transactionDate={Uri.EscapeDataString(date.ToString("o"))}&cartId=1&sessionId=fake-session";

        var response = await _noRedirectClient.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("success");
    }

    // --- PartialCheckoutSuccess ---

    [Fact]
    public async Task PartialCheckoutSuccess_ReturnsRedirectToSuccessUrl()
    {
        var date = DateTime.UtcNow;
        var url = $"/api/payments/partial-checkout-success?transactionDate={Uri.EscapeDataString(date.ToString("o"))}&cartId=1&sessionId=fake-session";

        var response = await _noRedirectClient.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("success");
    }

    // --- CheckoutFail ---

    [Fact]
    public async Task CheckoutFail_ReturnsRedirectToFailUrl()
    {
        var date = DateTime.UtcNow;
        var url = $"/api/payments/checkout-fail?transactionDate={Uri.EscapeDataString(date.ToString("o"))}&cartId=1&sessionId=fake-session";

        var response = await _noRedirectClient.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("fail");
    }
}
