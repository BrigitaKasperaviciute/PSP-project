using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

/// <summary>
/// Seeded state relevant to these tests:
///   Cart 3  Status=IN_PROGRESS  — used for cash transaction creation
///   Cart 4  Status=PENDING      — used for empty transaction list check
///   Cart 1  Status=PENDING      — used to verify refund rejection on non-COMPLETED cart
///   No transactions are seeded — all created by tests.
///
/// Full-checkout, partial-checkout, and card-refund endpoints are excluded because
/// they call Stripe's session and refund services which require live credentials.
/// </summary>
public class PaymentControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PaymentControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateAnonymousClient(); // Payment endpoints have no auth policies
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Helper — registers a cash transaction against Cart 3 (IN_PROGRESS → becomes COMPLETED)
    private async Task<TransactionResponse> CreateCashTransactionAsync()
    {
        var request = new CashRequest(
            CartId: 3,
            Amount: 2000,
            Tip: null,
            TransactionRef: "CASH_REF_001",
            PhoneNumber: null
        );
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TransactionResponse>(body, JsonOptions)!;
    }

    // ── POST /api/payments/cash ───────────────────────────────────────────────

    [Fact]
    public async Task Cash_WithInProgressCart_ReturnsOkAndCreatesTransaction()
    {
        // Arrange — Cart 3 is IN_PROGRESS
        var request = new CashRequest(
            CartId: 3,
            Amount: 5000,
            Tip: 200,
            TransactionRef: "CASH_001",
            PhoneNumber: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);
        var body = await response.Content.ReadAsStringAsync();
        var transaction = JsonSerializer.Deserialize<TransactionResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        transaction.Should().NotBeNull();
        transaction!.Amount.Should().Be(5000);
        transaction.Tip.Should().Be(200);
        transaction.TransactionRef.Should().StartWith("CASH_");
        transaction.Status.Should().Be(TransactionStatusEnum.CASH);
    }

    [Fact]
    public async Task Cash_WithNonExistentCart_ReturnsError()
    {
        // Arrange — cart 9999 does not exist
        var request = new CashRequest(
            CartId: 9999,
            Amount: 1000,
            Tip: null,
            TransactionRef: "CASH_ERR",
            PhoneNumber: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert — null cart reference or FK violation propagates as a server error
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    // ── GET /api/payments/{id:int} ────────────────────────────────────────────

    [Fact]
    public async Task GetTransactionsByCart_AfterCashTransaction_ReturnsOkWithTransactions()
    {
        // Arrange — create a transaction for Cart 3
        var created = await CreateCashTransactionAsync();

        // Act
        var response = await _client.GetAsync("/api/payments/3");
        var body = await response.Content.ReadAsStringAsync();
        var transactions = JsonSerializer.Deserialize<List<TransactionResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        transactions.Should().NotBeNull();
        transactions!.Should().Contain(t => t.TransactionRef == created.TransactionRef);
    }

    [Fact]
    public async Task GetTransactionsByCart_WithNoTransactions_ReturnsOkWithEmptyList()
    {
        // Arrange — Cart 4 is PENDING and has no transactions

        // Act
        var response = await _client.GetAsync("/api/payments/4");
        var body = await response.Content.ReadAsStringAsync();
        var transactions = JsonSerializer.Deserialize<List<TransactionResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        transactions.Should().NotBeNull();
        transactions!.Should().BeEmpty();
    }

    // ── PATCH /api/payments/refund/{id} ──────────────────────────────────────

    [Fact]
    public async Task Refund_CashTransaction_ReturnsOkAndMarksTransactionRefunded()
    {
        // Arrange — create a cash transaction; cart becomes COMPLETED after creation
        var transaction = await CreateCashTransactionAsync();
        var encodedId   = Uri.EscapeDataString(transaction.Id.ToString("o"));
        var refundRequest = new RefundRequest(CartId: 3, IsCard: false);

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/payments/refund/{encodedId}", refundRequest);
        var body = await response.Content.ReadAsStringAsync();
        var refunded = JsonSerializer.Deserialize<TransactionResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        refunded.Should().NotBeNull();
        refunded!.Status.Should().Be(TransactionStatusEnum.REFUNDED);
    }

    [Fact]
    public async Task Refund_WithNonCompletedCart_ReturnsError()
    {
        // Arrange — Cart 1 is PENDING; the service validates status == COMPLETED before refunding
        var fakeTransactionId = Uri.EscapeDataString(DateTime.UtcNow.AddYears(-1).ToString("o"));
        var refundRequest = new RefundRequest(CartId: 1, IsCard: false);

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/payments/refund/{fakeTransactionId}", refundRequest);

        // Assert — cart is not COMPLETED, service throws → not a success
        response.IsSuccessStatusCode.Should().BeFalse();
    }
}
