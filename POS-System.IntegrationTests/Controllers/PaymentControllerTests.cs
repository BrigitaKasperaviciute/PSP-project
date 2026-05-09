using System.Net;
using System.Text.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class PaymentControllerTests
{
    [Fact]
    public async Task RegisterCashTransaction_ValidRequest_UpdatesCartAndPersistsRefundableTransaction()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        var createCartResponse = await client.PostAsync("api/carts", TestDataFactory.ToJsonContent(new CartRequest { EmployeeVersionId = 0 }));
        createCartResponse.StatusCode.Should().Be(HttpStatusCode.OK, await createCartResponse.Content.ReadAsStringAsync());

        var cartId = factory.UseDbContext(db => db.Carts.OrderByDescending(cart => cart.Id).Select(cart => cart.Id).First());

        var cashRequest = new CashRequest(
            CartId: cartId,
            Amount: 2450,
            Tip: 150,
            TransactionRef: $"txn-{Guid.NewGuid():N}",
            PhoneNumber: null
        );

        // Act
        var cashResponse = await client.PostAsync("api/payments/cash", TestDataFactory.ToJsonContent(cashRequest));
        var cashBody = await cashResponse.Content.ReadAsStringAsync();
        var transactionResponse = JsonSerializer.Deserialize<TransactionResponse>(cashBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert
        cashResponse.StatusCode.Should().Be(HttpStatusCode.OK, cashBody);
        transactionResponse.Should().NotBeNull();
        transactionResponse!.TransactionRef.Should().Be($"CASH_{cashRequest.TransactionRef}");
        transactionResponse.Amount.Should().Be(cashRequest.Amount);
        transactionResponse.Status.Should().Be(TransactionStatusEnum.CASH);

        // Validate DB state
        var transactionId = factory.UseDbContext(db => db.Transactions.Single(transaction => transaction.TransactionRef == transactionResponse.TransactionRef).Id);

        factory.UseDbContext(db =>
        {
            var cart = db.Carts.Single(cart => cart.Id == cartId);
            cart.Status.Should().Be(CartStatusEnum.COMPLETED);

            var persistedTransaction = db.Transactions.Single(transaction => transaction.Id == transactionId);
            persistedTransaction.CartId.Should().Be(cartId);
            persistedTransaction.Status.Should().Be(TransactionStatusEnum.CASH);
            persistedTransaction.Amount.Should().Be(cashRequest.Amount);
        });

        // Act
        var refundRequest = new RefundRequest(cartId, false);
        var refundRouteValue = Uri.EscapeDataString(transactionId.ToString("O"));
        var refundResponse = await client.PatchAsync($"api/payments/refund/{refundRouteValue}", TestDataFactory.ToJsonContent(refundRequest));
        var refundBody = await refundResponse.Content.ReadAsStringAsync();
        var refundedTransaction = JsonSerializer.Deserialize<TransactionResponse>(refundBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert
        refundResponse.StatusCode.Should().Be(HttpStatusCode.OK, refundBody);
        refundedTransaction.Should().NotBeNull();
        refundedTransaction!.Status.Should().Be(TransactionStatusEnum.REFUNDED);
        refundedTransaction.TransactionRef.Should().Be($"CASH_{cashRequest.TransactionRef}");

        // Validate DB state
        factory.UseDbContext(db =>
        {
            db.Transactions.Single(transaction => transaction.Id == transactionId).Status.Should().Be(TransactionStatusEnum.REFUNDED);
            db.Carts.Single(cart => cart.Id == cartId).Status.Should().Be(CartStatusEnum.REFUNDED);
        });
    }

    [Fact]
    public async Task IssueRefund_InvalidCart_ReturnsNotFoundAndLeavesDatabaseUnchanged()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        var transactionId = Uri.EscapeDataString(DateTime.UtcNow.ToString("O"));
        var refundRequest = new RefundRequest(999999, false);

        // Act
        var response = await client.PatchAsync($"api/payments/refund/{transactionId}", TestDataFactory.ToJsonContent(refundRequest));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, body);
        body.Should().ContainEquivalentOf("not found");

        // Validate DB state
        factory.UseDbContext(db => db.Transactions.Count(transaction => transaction.CartId == refundRequest.CartId).Should().Be(0));
    }
}