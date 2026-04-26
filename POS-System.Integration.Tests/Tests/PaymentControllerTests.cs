using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class PaymentControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetTransactionsByCart_ExistingCart_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/payments/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterCashTransaction_ValidRequest_ReturnsOk()
    {
        var cartResponse = await _client.PostAsJsonAsync("/api/carts", new { EmployeeVersionId = 1 });
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();

        var request = new
        {
            CartId = cart!.Id,
            Amount = (ulong)5000,
            Tip = (int?)null,
            TransactionRef = "TEST_REF_001",
            PhoneNumber = (string?)null
        };

        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterCashTransaction_NonExistingCart_ReturnsNotFound()
    {
        var request = new
        {
            CartId = 99999,
            Amount = (ulong)1000,
            Tip = (int?)null,
            TransactionRef = "TEST_REF_FAIL",
            PhoneNumber = (string?)null
        };

        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IssueRefund_NonExistingCart_ReturnsNotFound()
    {
        var request = new { CartId = 99999, IsCard = false };

        var response = await _client.PatchAsJsonAsync("/api/payments/refund/2024-01-01", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
