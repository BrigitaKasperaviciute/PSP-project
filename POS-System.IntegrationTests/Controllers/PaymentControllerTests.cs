using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class PaymentControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public PaymentControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetTransactionsByCart_WithCashTransaction_ReturnsOkAndTransactions()
    {
        // Arrange
        var cart = await CreateCartAsync();
        await RegisterCashTransactionAsync(cart.Id);

        // Act
        var response = await _client.GetAsync($"/api/payments/{cart.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        body.Should().NotBeNull();
        var single = body!.Single();
        single.Status.Should().Be(TransactionStatusEnum.CASH);

        await using var db = _factory.CreateDbContext();
        var persistedTransaction = await db.Transactions.AsNoTracking().SingleAsync(item => item.CartId == cart.Id);
        persistedTransaction.Status.Should().Be(TransactionStatusEnum.CASH);
    }

    [Fact]
    public async Task RegisterCashTransaction_WithValidPayload_ReturnsOkAndPersistsCompletedCart()
    {
        // Arrange
        var cart = await CreateCartAsync();
        var request = new CashRequestBuilder()
            .WithCartId(cart.Id)
            .WithAmount(1500)
            .WithTransactionRef($"CASH-{Guid.NewGuid():N}")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
        body!.Amount.Should().Be(1500);
        body.Status.Should().Be(TransactionStatusEnum.CASH);
        body.TransactionRef.Should().StartWith("CASH_");

        await using var db = _factory.CreateDbContext();
        var persistedCart = await db.Carts.AsNoTracking().SingleAsync(item => item.Id == cart.Id);
        persistedCart.Status.Should().Be(CartStatusEnum.COMPLETED);

        var persistedTransaction = await db.Transactions.AsNoTracking().SingleAsync(item => item.CartId == cart.Id);
        persistedTransaction.Status.Should().Be(TransactionStatusEnum.CASH);
        persistedTransaction.Amount.Should().Be(1500);
    }

    [Fact]
    public async Task RegisterCashTransaction_WithMissingCart_ReturnsInternalServerError()
    {
        // Arrange
        var request = new CashRequestBuilder()
            .WithCartId(999999)
            .WithAmount(1000)
            .WithTransactionRef($"CASH-{Guid.NewGuid():N}")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task IssueRefund_WithCompletedCashTransaction_ReturnsOkAndRefundsTransaction()
    {
        // Arrange
        var cart = await CreateCartAsync();
        var transaction = await RegisterCashTransactionAsync(cart.Id);
        var refundRequest = new RefundRequest(cart.Id, false);
        var transactionId = WebUtility.UrlEncode(transaction.Id.ToString("O"));

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/payments/refund/{transactionId}", refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(TransactionStatusEnum.REFUNDED);
        body.TransactionRef.Should().Be(transaction.TransactionRef);

        await using var db = _factory.CreateDbContext();
        var persistedCart = await db.Carts.AsNoTracking().SingleAsync(item => item.Id == cart.Id);
        persistedCart.Status.Should().Be(CartStatusEnum.REFUNDED);

        var persistedTransaction = await db.Transactions.AsNoTracking().SingleAsync(item => item.Id == transaction.Id);
        persistedTransaction.Status.Should().Be(TransactionStatusEnum.REFUNDED);
    }

    [Fact]
    public async Task IssueRefund_WithOpenCart_ReturnsBadRequestAndKeepsTransactionPending()
    {
        // Arrange
        var cart = await CreateCartAsync();
        var refundRequest = new RefundRequest(cart.Id, false);
        var transactionId = WebUtility.UrlEncode(DateTime.UtcNow.ToString("O"));

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/payments/refund/{transactionId}", refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = _factory.CreateDbContext();
        var persistedCart = await db.Carts.AsNoTracking().SingleAsync(item => item.Id == cart.Id);
        persistedCart.Status.Should().Be(CartStatusEnum.IN_PROGRESS);
        (await db.Transactions.AsNoTracking().CountAsync(item => item.CartId == cart.Id)).Should().Be(0);
    }

    private async Task<CartResponse> CreateCartAsync()
    {
        var employee = await CreateEmployeeAsync();
        var createRequest = new CartRequestBuilder()
            .WithEmployeeVersionId(employee.Id)
            .Build();

        var response = await _client.PostAsJsonAsync("/api/carts", createRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.EmployeeVersionId.Should().Be(employee.Id);
        body.Status.Should().Be(CartStatusEnum.IN_PROGRESS);

        return body;
    }

    private async Task<EmployeeResponse> CreateEmployeeAsync()
    {
        var request = new POS_System.Business.Dtos.Request.UserRegisterRequest(
            Email: $"employee-{Guid.NewGuid():N}@example.com",
            UserName: $"employee-{Guid.NewGuid():N}",
            FirstName: "Test",
            LastName: "Employee",
            Password: "Test@1234!",
            PhoneNumber: "+1234567890",
            BirthDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25)),
            RoleId: 1
        );

        var response = await _client.PostAsJsonAsync("/api/employees/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        return body!;
    }

    private async Task<TransactionResponse> RegisterCashTransactionAsync(int cartId)
    {
        var request = new CashRequestBuilder()
            .WithCartId(cartId)
            .WithAmount(1500)
            .WithTransactionRef($"CASH-{Guid.NewGuid():N}")
            .Build();

        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
        return body!;
    }
}
