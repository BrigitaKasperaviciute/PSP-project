using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Business.Services.Interfaces;
using POS_System.Common.Enums;

namespace POS_System.IntegrationTests.Infrastructure;

public sealed class FakePaymentService : IPaymentService
{
    public static readonly TransactionResponse FakeTransaction = new(
        DateTime.UtcNow,
        1000UL,
        null,
        "fake-ref",
        TransactionStatusEnum.CASH
    );

    public Task<TransactionResponse> RegisterCashTransactionAsync(CashRequest cashRequest, CancellationToken token)
        => Task.FromResult(FakeTransaction);

    public Task<List<TransactionResponse>> GetTransactionsByCartAsync(int cartId)
        => Task.FromResult(new List<TransactionResponse> { FakeTransaction });

    public Task<TransactionResponse> IssueRefundAsync(DateTime transactionId, RefundRequest refundRequest, CancellationToken token)
        => Task.FromResult(FakeTransaction with { Status = TransactionStatusEnum.REFUNDED });

    public Task<CheckoutResponse> FullCheckoutAsync(CheckoutRequest checkoutRequest, CancellationToken token)
        => Task.FromResult(new CheckoutResponse("fake-session-id", "fake-pub-key"));

    public Task<PartialCheckoutResponse> InitializePartialCheckoutAsync(InitPartialCheckoutRequest checkoutRequest, CancellationToken token)
        => Task.FromResult(new PartialCheckoutResponse(new List<TransactionResponse> { FakeTransaction }));

    public Task<CheckoutResponse> PartialCheckoutAsync(PartialCheckoutRequest checkoutRequest, CancellationToken token)
        => Task.FromResult(new CheckoutResponse("fake-session-id", "fake-pub-key"));

    public Task<string> FullCheckoutSuccessAsync(DateTime transactionDate, string sessionId, int cartId, string? phoneNumber)
        => Task.FromResult("http://localhost:3001/success");

    public Task<string> PartialCheckoutSuccessAsync(DateTime transactionDate, string sessionId, int cartId, string? phoneNumber)
        => Task.FromResult("http://localhost:3001/success");

    public Task<string> CheckoutFailAsync(DateTime transactionDate, string sessionId, int cartId, string? giftCardCode, long? discount)
        => Task.FromResult("http://localhost:3001/fail");
}
