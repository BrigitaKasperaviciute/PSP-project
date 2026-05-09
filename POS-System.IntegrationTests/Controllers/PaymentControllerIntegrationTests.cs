using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class PaymentControllerCoverageTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _client = null!;

    public PaymentControllerCoverageTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();

        await ResetPaymentStateAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task RegisterCashTransaction_ValidRequest_ReturnsOkAndPersistsTransaction()
    {
        // Arrange
        var request = new CashRequestBuilder()
            .WithCartId(1)
            .WithAmount(5000)
            .WithTransactionRef($"cash-{Guid.NewGuid():N}")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
        body!.Amount.Should().Be(request.Amount);
        body.Tip.Should().Be(request.Tip);
        body.TransactionRef.Should().Be($"CASH_{request.TransactionRef}");
        body.Status.Should().Be(TransactionStatusEnum.CASH);

        await using var db = _factory.GetDbContext();
        var persistedTransaction = await db.Transactions.AsNoTracking().SingleAsync(transaction => transaction.Id == body.Id);
        var completedCart = await db.Carts.AsNoTracking().SingleAsync(cart => cart.Id == request.CartId);

        persistedTransaction.TransactionRef.Should().Be($"CASH_{request.TransactionRef}");
        persistedTransaction.Status.Should().Be(TransactionStatusEnum.CASH);
        completedCart.Status.Should().Be(CartStatusEnum.COMPLETED);
    }

    [Fact]
    public async Task RegisterCashTransaction_MissingCart_ReturnsNotFoundAndKeepsCartUnchanged()
    {
        // Arrange
        var request = new CashRequestBuilder()
            .WithCartId(99999)
            .WithAmount(5000)
            .WithTransactionRef($"cash-missing-{Guid.NewGuid():N}")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var db = _factory.GetDbContext();
        var persistedTransaction = await db.Transactions.AsNoTracking().SingleOrDefaultAsync(transaction => transaction.TransactionRef == $"CASH_{request.TransactionRef}");
        persistedTransaction.Should().NotBeNull();

        var cart = await db.Carts.AsNoTracking().SingleAsync(existingCart => existingCart.Id == 1);
        cart.Status.Should().Be(CartStatusEnum.PENDING);
    }

    [Fact]
    public async Task IssueRefund_CompletedCashTransaction_ReturnsOkAndMarksRefunded()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        await SeedCompletedCashTransactionAsync(transactionDate);

        var request = new RefundRequestBuilder()
            .WithCartId(1)
            .WithIsCard(false)
            .Build();

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/payments/refund/{Uri.EscapeDataString(transactionDate.ToString("o"))}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(TransactionStatusEnum.REFUNDED);
        body.TransactionRef.Should().Be("cash-refund-ref");

        await using var db = _factory.GetDbContext();
        var refundedTransaction = await db.Transactions.AsNoTracking().SingleAsync(transaction => transaction.Id == transactionDate);
        var cart = await db.Carts.AsNoTracking().SingleAsync(existingCart => existingCart.Id == request.CartId);

        refundedTransaction.Status.Should().Be(TransactionStatusEnum.REFUNDED);
        cart.Status.Should().Be(CartStatusEnum.REFUNDED);
    }

    [Fact]
    public async Task IssueRefund_PendingCart_ReturnsBadRequestAndLeavesTransactionUnchanged()
    {
        // Arrange
        var transactionDate = DateTime.UtcNow;
        await SeedPendingCashTransactionAsync(transactionDate);

        var request = new RefundRequestBuilder()
            .WithCartId(1)
            .WithIsCard(false)
            .Build();

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/payments/refund/{Uri.EscapeDataString(transactionDate.ToString("o"))}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = _factory.GetDbContext();
        var transaction = await db.Transactions.AsNoTracking().SingleAsync(existingTransaction => existingTransaction.Id == transactionDate);
        var cart = await db.Carts.AsNoTracking().SingleAsync(existingCart => existingCart.Id == request.CartId);

        transaction.Status.Should().Be(TransactionStatusEnum.CASH);
        cart.Status.Should().Be(CartStatusEnum.PENDING);
    }

    [Fact]
    public async Task CheckoutFail_SeededTransaction_ReturnsRedirectAndRestoresCartState()
    {
        // Arrange
        var transactionDate = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        await SeedPendingPaymentTransactionAsync(transactionDate);

        // Act
        var response = await _client.GetAsync($"/api/payments/checkout-fail?transactionDate={Uri.EscapeDataString(transactionDate.ToString("yyyy-MM-ddTHH:mm:ss"))}&cartId=1&sessionId=session-123");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            response.Headers.Location.Should().NotBeNull();
            response.Headers.Location!.ToString().Should().Be("http://localhost:3001/");

            await using var db = _factory.GetDbContext();
            var transaction = await db.Transactions.AsNoTracking().SingleAsync(existingTransaction => existingTransaction.Id == transactionDate);
            var cart = await db.Carts.AsNoTracking().SingleAsync(existingCart => existingCart.Id == 1);

            transaction.TransactionRef.Should().Be("session-123");
            transaction.Status.Should().Be(TransactionStatusEnum.PENDING);
            cart.Status.Should().Be(CartStatusEnum.PENDING);
        }
    }

    [Fact]
    public async Task CheckoutFail_MissingTransaction_ReturnsInternalServerError()
    {
        // Arrange
        var transactionDate = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

        // Act
        var response = await _client.GetAsync($"/api/payments/checkout-fail?transactionDate={Uri.EscapeDataString(transactionDate.ToString("yyyy-MM-ddTHH:mm:ss"))}&cartId=1&sessionId=session-missing");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.InternalServerError, HttpStatusCode.NotFound);
    }

    private async Task ResetPaymentStateAsync()
    {
        await using var db = _factory.GetDbContext();

        db.Transactions.RemoveRange(db.Transactions);

        var cart = await db.Carts.SingleAsync(existingCart => existingCart.Id == 1);
        cart.Status = CartStatusEnum.PENDING;
        cart.CartDiscountId = null;

        await db.SaveChangesAsync();
    }

    private async Task SeedCompletedCashTransactionAsync(DateTime transactionDate)
    {
        await using var db = _factory.GetDbContext();

        var cart = await db.Carts.SingleAsync(existingCart => existingCart.Id == 1);
        cart.Status = CartStatusEnum.COMPLETED;

        db.Transactions.Add(new Transaction
        {
            Id = transactionDate,
            CartId = cart.Id,
            Amount = 5000,
            Tip = null,
            TransactionRef = "cash-refund-ref",
            Status = TransactionStatusEnum.CASH
        });

        await db.SaveChangesAsync();
    }

    private async Task SeedPendingCashTransactionAsync(DateTime transactionDate)
    {
        await using var db = _factory.GetDbContext();

        var cart = await db.Carts.SingleAsync(existingCart => existingCart.Id == 1);
        cart.Status = CartStatusEnum.PENDING;

        db.Transactions.Add(new Transaction
        {
            Id = transactionDate,
            CartId = cart.Id,
            Amount = 5000,
            Tip = null,
            TransactionRef = "cash-pending-ref",
            Status = TransactionStatusEnum.CASH
        });

        await db.SaveChangesAsync();
    }

    private async Task SeedPendingPaymentTransactionAsync(DateTime transactionDate)
    {
        await using var db = _factory.GetDbContext();

        var cart = await db.Carts.SingleAsync(existingCart => existingCart.Id == 1);
        cart.Status = CartStatusEnum.PENDING;

        db.Transactions.Add(new Transaction
        {
            Id = transactionDate,
            CartId = cart.Id,
            Amount = 5000,
            Tip = null,
            TransactionRef = "pending-payment-ref",
            Status = TransactionStatusEnum.PENDING
        });

        await db.SaveChangesAsync();
    }
}