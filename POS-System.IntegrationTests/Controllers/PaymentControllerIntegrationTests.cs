using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class PaymentControllerIntegrationTests
{
    [Fact]
    public async Task IssueRefund_NonCompletedCart_ReturnsBadRequest()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CashWrite" });

        var transactionDate = DateTime.UtcNow;
        var encodedTransactionDate = Uri.EscapeDataString(transactionDate.ToString("O"));

        var response = await client.PatchAsJsonAsync($"/api/payments/refund/{encodedTransactionDate}", new RefundRequest(1, false));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IssueRefund_CashTransactionWithoutCardGateway_RefundsTransactionAndCart()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CashWrite" });

        var createResponse = await client.PostAsJsonAsync("/api/payments/cash", new CashRequest(
            CartId: 1,
            Amount: 1500u,
            Tip: null,
            TransactionRef: "refund-test",
            PhoneNumber: null
        ));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createdTransaction = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>();
        createdTransaction.Should().NotBeNull();

        var encodedTransactionDate = Uri.EscapeDataString(createdTransaction!.Id.ToString("O"));
        var refundResponse = await client.PatchAsJsonAsync($"/api/payments/refund/{encodedTransactionDate}", new RefundRequest(1, false));

        refundResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refundedTransaction = await refundResponse.Content.ReadFromJsonAsync<TransactionResponse>();
        refundedTransaction.Should().NotBeNull();
        refundedTransaction!.Status.Should().Be(TransactionStatusEnum.REFUNDED);

        var cart = await factory.ExecuteDbContextAsync(db => db.Carts.SingleAsync(x => x.Id == 1));
        cart.Status.Should().Be(CartStatusEnum.REFUNDED);
    }

    [Fact]
    public async Task FullCheckout_CompletedCart_ReturnsBadRequest()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(Array.Empty<string>());

        var payload = new CheckoutRequest(
            CartId: 2,
            EmployeeId: 2,
            Tip: null,
            PhoneNumber: null,
            CartItems: [ new CheckoutCartItem("Coffee", "Hot coffee", 199, 1, null) ]
        );

        var response = await client.PostAsJsonAsync("/api/payments/full-checkout", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InitializePartialCheckout_TooManyPayments_ReturnsBadRequest()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(Array.Empty<string>());

        var payload = new InitPartialCheckoutRequest(
            CartId: 3,
            EmployeeId: 2,
            PaymentCount: 999,
            Tip: null,
            CartItems: [ new CheckoutCartItem("Coffee", "Hot coffee", 199, 1, null) ]
        );

        var response = await client.PostAsJsonAsync("/api/payments/init-partial-checkout", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PartialCheckout_NonPendingTransaction_ReturnsBadRequest()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CashWrite" });

        var createResponse = await client.PostAsJsonAsync("/api/payments/cash", new CashRequest(
            CartId: 1,
            Amount: 1200u,
            Tip: null,
            TransactionRef: "partial-checkout-test",
            PhoneNumber: null
        ));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createdTransaction = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>();
        createdTransaction.Should().NotBeNull();

        var payload = new PartialCheckoutRequest(
            CartId: 1,
            Id: createdTransaction!.Id,
            GiftCard: null,
            PhoneNumber: null
        );

        var response = await client.PostAsJsonAsync("/api/payments/partial-checkout", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterCashTransaction_ValidPayloadPersistsTransactionAndCompletesCart()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CashWrite" });

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Transactions.CountAsync());
        var payload = new CashRequest(
            CartId: 1,
            Amount: 1500u,
            Tip: 100,
            TransactionRef: "integration-cash-1",
            PhoneNumber: null
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/payments/cash", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        created.Should().NotBeNull();
        created!.Amount.Should().Be((ulong)payload.Amount);
        created.Status.Should().Be(TransactionStatusEnum.CASH);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Transactions.CountAsync());
        countAfter.Should().Be(countBefore + 1);

        var persistedCart = await factory.ExecuteDbContextAsync(db => db.Carts.SingleAsync(c => c.Id == payload.CartId));
        persistedCart.Status.Should().Be(CartStatusEnum.COMPLETED);
    }

    [Fact]
    public async Task GetTransactionsByCart_ExistingCartReturnsTransactions()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "CashRead" });

        // Arrange - create one transaction first
        var payload = new CashRequest(
            CartId: 2,
            Amount: 2000u,
            Tip: null,
            TransactionRef: "integration-cash-2",
            PhoneNumber: null
        );

        var createResp = await client.PostAsJsonAsync("/api/payments/cash", payload);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var response = await client.GetAsync($"/api/payments/{payload.CartId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        list.Should().NotBeNull();
        list!.Should().ContainSingle(t => t.TransactionRef.Contains("integration-cash-2") || t.Amount == payload.Amount);
    }
}
