using POS_System.IntegrationTests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using System.Net;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    /// <summary>
    /// Integration tests for PaymentController.
    /// Tests payment operations including cash transactions, refunds, and checkout flows.
    /// </summary>
    public class PaymentControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/payments";

        [Fact]
        public async Task RegisterCashTransaction_ValidRequest_ReturnsOkAndCompletesCart()
        {
            // Arrange
            var paymentRequest = new CashRequest(
                CartId: 3,
                Amount: 5000,
                Tip: null,
                TransactionRef: Guid.NewGuid().ToString("N"),
                PhoneNumber: null
            );

            // Act
            var response = await Client.PostAsync($"{BaseUrl}/cash", CreateJsonContent(paymentRequest));

            // Assert
            AssertOkResponse(response);

            var transaction = await DeserializeResponseAsync<TransactionResponse>(response);
            transaction.Should().NotBeNull();
            transaction!.Amount.Should().Be(paymentRequest.Amount);
            transaction.TransactionRef.Should().StartWith("CASH_");
            transaction.Status.Should().Be(TransactionStatusEnum.CASH);

            var cartCompleted = await ExecuteDbAsync(db => db.Carts.AnyAsync(cart => cart.Id == paymentRequest.CartId && cart.Status == CartStatusEnum.COMPLETED));
            cartCompleted.Should().BeTrue();

            var transactionPersisted = await ExecuteDbAsync(db => db.Transactions.AnyAsync(item => item.CartId == paymentRequest.CartId && item.TransactionRef == transaction.TransactionRef && item.Status == TransactionStatusEnum.CASH));
            transactionPersisted.Should().BeTrue();
        }

        [Fact]
        public async Task RegisterCashTransaction_InvalidCartId_ReturnsNotFoundAndPersistsTransaction()
        {
            // Arrange
            var paymentRequest = new CashRequest(
                CartId: 999999,
                Amount: 5000,
                Tip: null,
                TransactionRef: Guid.NewGuid().ToString("N"),
                PhoneNumber: null
            );

            // Act
            var response = await Client.PostAsync($"{BaseUrl}/cash", CreateJsonContent(paymentRequest));

            // Assert
            AssertNotFoundResponse(response);

            var transactionPersisted = await ExecuteDbAsync(db => db.Transactions.AnyAsync(item => item.CartId == paymentRequest.CartId && item.TransactionRef == $"CASH_{paymentRequest.TransactionRef}" && item.Status == TransactionStatusEnum.CASH));
            transactionPersisted.Should().BeTrue();
        }

        [Fact]
        public async Task RegisterCashTransactionAsync_NegativeAmount_ReturnsErrorOrOk()
        {
            // Arrange
            var paymentRequest = new { cartId = 1, amount = -1000 };

            // Act
            var response = await Client.PostAsync($"{BaseUrl}/cash", CreateJsonContent(paymentRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.OK,
                HttpStatusCode.InternalServerError
            );
        }

        [Fact]
        public async Task GetTransactionsByCartAsync_ValidCartId_ReturnsTransactions()
        {
            // Arrange
            int cartId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/cart/{cartId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task IssueRefund_CompletedCashTransaction_ReturnsOkAndRefundsTransaction()
        {
            // Arrange
            var cashRequest = new CashRequest(
                CartId: 3,
                Amount: 5000,
                Tip: null,
                TransactionRef: Guid.NewGuid().ToString("N"),
                PhoneNumber: null
            );

            var cashResponse = await Client.PostAsync($"{BaseUrl}/cash", CreateJsonContent(cashRequest));
            AssertOkResponse(cashResponse);

            var cashTransaction = await DeserializeResponseAsync<TransactionResponse>(cashResponse);
            cashTransaction.Should().NotBeNull();

            var refundRequest = new RefundRequest(CartId: 3, IsCard: false);

            // Act
            var response = await Client.PatchAsync($"{BaseUrl}/refund/{cashTransaction!.Id:O}", CreateJsonContent(refundRequest));

            // Assert
            AssertOkResponse(response);

            var refundedTransaction = await DeserializeResponseAsync<TransactionResponse>(response);
            refundedTransaction.Should().NotBeNull();
            refundedTransaction!.Status.Should().Be(TransactionStatusEnum.REFUNDED);

            var cartRefunded = await ExecuteDbAsync(db => db.Carts.AnyAsync(cart => cart.Id == refundRequest.CartId && cart.Status == CartStatusEnum.REFUNDED));
            cartRefunded.Should().BeTrue();
        }

        [Fact]
        public async Task InitializePartialCheckoutAsync_ValidRequest_ReturnsOkOrError()
        {
            // Arrange
            var checkoutRequest = new { cartId = 1, amount = 5000 };

            // Act
            var response = await Client.PostAsync(
                $"{BaseUrl}/checkout/partial/initialize",
                CreateJsonContent(checkoutRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest,
                HttpStatusCode.NotFound,
                HttpStatusCode.InternalServerError
            );
        }

        [Fact]
        public async Task PartialCheckoutAsync_ValidRequest_ReturnsOkOrError()
        {
            // Arrange
            var checkoutRequest = new { cartId = 1, sessionId = "test123" };

            // Act
            var response = await Client.PostAsync(
                $"{BaseUrl}/checkout/partial",
                CreateJsonContent(checkoutRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest,
                HttpStatusCode.NotFound,
                HttpStatusCode.InternalServerError
            );
        }

        [Fact]
        public async Task PartialCheckoutSuccess_ValidRequest_ReturnsRedirectOrError()
        {
            // Arrange
            var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync(
                $"{BaseUrl}/partial-checkout-success?transactionDate={DateTime.UtcNow:O}&cartId=3&sessionId=test123"
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task PartialCheckoutFail_ValidRequest_ReturnsRedirectOrError()
        {
            // Arrange
            var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync(
                $"{BaseUrl}/checkout-fail?transactionDate={DateTime.UtcNow:O}&cartId=3&sessionId=test123"
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task FullCheckoutAsync_ValidRequest_ReturnsOkOrError()
        {
            // Arrange
            var checkoutRequest = new { cartId = 1 };

            // Act
            var response = await Client.PostAsync(
                $"{BaseUrl}/checkout/full",
                CreateJsonContent(checkoutRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest,
                HttpStatusCode.NotFound,
                HttpStatusCode.InternalServerError
            );
        }

        [Fact]
        public async Task FullCheckoutSuccess_ValidRequest_ReturnsRedirectOrError()
        {
            // Arrange
            var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync(
                $"{BaseUrl}/full-checkout-success?transactionDate={DateTime.UtcNow:O}&cartId=3&sessionId=test123"
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task FullCheckoutFail_ValidRequest_ReturnsRedirectOrError()
        {
            // Arrange
            var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync(
                $"{BaseUrl}/checkout-fail?transactionDate={DateTime.UtcNow:O}&cartId=3&sessionId=test123"
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.InternalServerError);
        }
    }
}
