using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class PaymentControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public PaymentControllerTests(ApiLayerTestApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });
    }

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    // --- Cash payment ---

    [Fact]
    public async Task RegisterCashTransaction_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/payments/cash",
            Json("{\"cartId\":1,\"amount\":1000,\"tip\":50,\"transactionRef\":\"ref-1\",\"phoneNumber\":\"+37060000001\"}"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task RegisterCashTransaction_ServiceThrowsBadRequest_Returns400()
    {
        _factory.PaymentServiceMock
            .Setup(x => x.RegisterCashTransactionAsync(It.IsAny<CashRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Cart already paid"));

        var response = await _client.PostAsync("/api/payments/cash",
            Json("{\"cartId\":1,\"amount\":1000,\"tip\":50,\"transactionRef\":\"ref-1\",\"phoneNumber\":null}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Refund ---

    [Fact]
    public async Task IssueRefund_ValidRequest_Returns200()
    {
        var response = await _client.PatchAsync("/api/payments/refund/2025-01-01T00:00:00",
            Json("{\"cartId\":1,\"isCard\":false}"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task IssueRefund_ServiceThrowsNotFound_Returns404()
    {
        _factory.PaymentServiceMock
            .Setup(x => x.IssueRefundAsync(It.IsAny<DateTime>(), It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Transaction not found"));

        var response = await _client.PatchAsync("/api/payments/refund/2025-01-01T00:00:00",
            Json("{\"cartId\":999,\"isCard\":false}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetTransactionsByCart ---

    [Fact]
    public async Task GetTransactionsByCart_ExistingCart_Returns200()
    {
        var response = await _client.GetAsync("/api/payments/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetTransactionsByCart_ServiceThrowsNotFound_Returns404()
    {
        _factory.PaymentServiceMock
            .Setup(x => x.GetTransactionsByCartAsync(It.IsAny<int>()))
            .ThrowsAsync(new NotFoundException("Cart not found"));

        var response = await _client.GetAsync("/api/payments/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- FullCheckout ---

    [Fact]
    public async Task FullCheckout_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/payments/full-checkout",
            Json("{\"cartId\":1,\"employeeId\":1,\"tip\":0,\"phoneNumber\":null,\"cartItems\":[{\"name\":\"Item\",\"description\":\"Desc\",\"price\":100,\"quantity\":1,\"imageURL\":null}]}"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task FullCheckout_ServiceThrowsBadRequest_Returns400()
    {
        _factory.PaymentServiceMock
            .Setup(x => x.FullCheckoutAsync(It.IsAny<CheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Cart is empty"));

        var response = await _client.PostAsync("/api/payments/full-checkout",
            Json("{\"cartId\":1,\"employeeId\":1,\"tip\":0,\"phoneNumber\":null,\"cartItems\":[]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- InitPartialCheckout ---

    [Fact]
    public async Task InitPartialCheckout_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/payments/init-partial-checkout",
            Json("{\"cartId\":1,\"employeeId\":1,\"paymentCount\":2,\"tip\":0,\"cartItems\":[{\"name\":\"Item\",\"description\":\"Desc\",\"price\":100,\"quantity\":1,\"imageURL\":null}]}"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task InitPartialCheckout_ServiceThrowsBadRequest_Returns400()
    {
        _factory.PaymentServiceMock
            .Setup(x => x.InitializePartialCheckoutAsync(It.IsAny<InitPartialCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Payment count must be > 1"));

        var response = await _client.PostAsync("/api/payments/init-partial-checkout",
            Json("{\"cartId\":1,\"employeeId\":1,\"paymentCount\":1,\"tip\":0,\"cartItems\":[]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- PartialCheckout ---

    [Fact]
    public async Task PartialCheckout_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/payments/partial-checkout",
            Json("{\"cartId\":1,\"id\":\"2025-01-01T00:00:00Z\",\"giftCard\":null,\"phoneNumber\":null}"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task PartialCheckout_ServiceThrowsNotFound_Returns404()
    {
        _factory.PaymentServiceMock
            .Setup(x => x.PartialCheckoutAsync(It.IsAny<PartialCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Partial checkout session not found"));

        var response = await _client.PostAsync("/api/payments/partial-checkout",
            Json("{\"cartId\":1,\"id\":\"2020-01-01T00:00:00Z\",\"giftCard\":null,\"phoneNumber\":null}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Redirect endpoints ---

    [Fact]
    public async Task FullCheckoutSuccess_ServiceReturnsPath_Redirects()
    {
        var response = await _client.GetAsync(
            "/api/payments/full-checkout-success?transactionDate=2025-01-01T00:00:00&cartId=1&sessionId=sess-1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task FullCheckoutSuccess_ServiceThrowsNotFound_Returns404()
    {
        _factory.PaymentServiceMock
            .Setup(x => x.FullCheckoutSuccessAsync(It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ThrowsAsync(new NotFoundException("Session not found"));

        var response = await _client.GetAsync(
            "/api/payments/full-checkout-success?transactionDate=2025-01-01T00:00:00&cartId=1&sessionId=bad");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PartialCheckoutSuccess_ServiceReturnsPath_Redirects()
    {
        var response = await _client.GetAsync(
            "/api/payments/partial-checkout-success?transactionDate=2025-01-01T00:00:00&cartId=1&sessionId=sess-1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task PartialCheckoutSuccess_ServiceThrowsNotFound_Returns404()
    {
        _factory.PaymentServiceMock
            .Setup(x => x.PartialCheckoutSuccessAsync(It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ThrowsAsync(new NotFoundException("Session not found"));

        var response = await _client.GetAsync(
            "/api/payments/partial-checkout-success?transactionDate=2025-01-01T00:00:00&cartId=1&sessionId=bad");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CheckoutFail_ServiceReturnsPath_Redirects()
    {
        var response = await _client.GetAsync(
            "/api/payments/checkout-fail?transactionDate=2025-01-01T00:00:00&cartId=1&sessionId=sess-1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task CheckoutFail_ServiceThrowsNotFound_Returns404()
    {
        _factory.PaymentServiceMock
            .Setup(x => x.CheckoutFailAsync(It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<long?>()))
            .ThrowsAsync(new NotFoundException("Session not found"));

        var response = await _client.GetAsync(
            "/api/payments/checkout-fail?transactionDate=2025-01-01T00:00:00&cartId=1&sessionId=bad");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
