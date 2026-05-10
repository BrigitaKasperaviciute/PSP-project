using System.Net;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class PaymentControllerTests(PosWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // GET /api/payments/{id}
    [Fact]
    public async Task GetTransactionsByCart_ExistingCart_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/payments/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactionsByCart_NonExistentCart_ReturnsOkEmptyList()
    {
        var response = await _client.GetAsync("/api/payments/99999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // POST /api/payments/cash
    [Fact]
    public async Task RegisterCashTransaction_ValidRequest_ReturnsOk()
    {
        // Create a fresh cart in IN_PROGRESS state
        var cartResp = await _client.PostAsJsonAsync("/api/carts", new CartRequest { EmployeeVersionId = 1 });
        var cart = await cartResp.Content.ReadFromJsonAsync<CartIdResponse>();

        var request = new CashRequest(
            CartId: cart!.Id,
            Amount: 5000,
            Tip: null,
            TransactionRef: $"TEST_{Guid.NewGuid():N}",
            PhoneNumber: null);

        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterCashTransaction_NonExistentCart_ReturnsInternalServerError()
    {
        var request = new CashRequest(
            CartId: 99999,
            Amount: 5000,
            Tip: null,
            TransactionRef: "TEST_NONEXISTENT",
            PhoneNumber: null);

        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private record CartIdResponse(int Id);
}
