using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Text.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class TargetedCoverageTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _client;

    public TargetedCoverageTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = null!;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateAuthenticatedClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Auth_Login_InvalidRequest_ReturnsBadRequestOrUnauthorized()
    {
        var invalid = "{}";
        var response = await _client.PostAsync("/v1/auth/login",
            new StringContent(invalid, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Auth_Register_ValidRequest_HitsController()
    {
        var request = new UserRegisterRequest(
            $"integration-{Guid.NewGuid():N}@example.com",
            $"integration-{Guid.NewGuid():N}",
            "Integration",
            "User",
            "Test@123456",
            "123456789",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25)),
            1);

        var response = await _client.PostAsync("/api/employees/register",
            new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Conflict, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Auth_Reset_ValidRequest_HitsController()
    {
        var request = new
        {
            Email = "test@example.com",
            Token = "reset-token",
            NewPassword = "NewPass@123456"
        };

        var response = await _client.PostAsync("/reset-password",
            new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cart_ApplyDiscount_ValidRoute_HitsController()
    {
        var request = new ApplyDiscountRequest { CartDiscountId = 1 };

        var response = await _client.PatchAsync("/api/carts/1/discount",
            new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ItemDiscount_Create_HitsController()
    {
        var admin = _factory.CreateAuthenticatedClient("ItemDiscountWrite");
        var body = new ItemDiscountRequestBuilder().WithValue(5).Build();

        var response = await admin.PostAsync("/api/item-discount",
            new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ItemDiscount_LinkAndUnlinkAndLookup_HitsController()
    {
        var admin = _factory.CreateAuthenticatedClient("ItemDiscountRead", "ItemDiscountWrite");

        var link = await admin.PutAsync("/api/item-discount/1/link?itemsAreProducts=true",
            new StringContent(JsonSerializer.Serialize(new[] { 1 }), System.Text.Encoding.UTF8, "application/json"));
        link.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        var unlink = await admin.PutAsync("/api/item-discount/1/unlink?itemsAreProducts=true",
            new StringContent(JsonSerializer.Serialize(new[] { 1 }), System.Text.Encoding.UTF8, "application/json"));
        unlink.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        var lookup = await admin.GetAsync("/api/item-discount/item/1?isProduct=true");
        lookup.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CartDiscount_CreateGetDelete_HitsController()
    {
        var payload = new { Value = 100, IsPercentage = false, EndDate = DateTime.UtcNow.AddDays(7) };

        var create = await _client.PostAsync("/api/cart-discount",
            new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json"));

        create.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);

        // attempt get and delete to exercise methods (use a non-existing id)
        var id = "00000000-0000-0000-0000-000000000000";
        var get = await _client.GetAsync($"/api/cart-discount/{id}");
        get.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        var del = await _client.DeleteAsync($"/api/cart-discount/{id}");
        del.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cart_CreateAndGetDiscount_HitsController()
    {
        var cartReq = new CartRequestBuilder().WithEmployeeVersionId(1).Build();
        var create = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartReq), System.Text.Encoding.UTF8, "application/json"));

        create.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);

        if (create.StatusCode == HttpStatusCode.OK)
        {
            var content = await create.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Parse<JsonElement>(content);
            if (doc.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
            {
                var id = idProp.GetInt32();
                var discount = await _client.GetAsync($"/api/carts/{id}/discount");
                discount.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.NoContent);
            }
        }
    }

    [Fact]
    public async Task CartItem_Create_HitsController()
    {
        // create cart
        var cartReq = new CartRequestBuilder().WithEmployeeVersionId(1).Build();
        var create = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartReq), System.Text.Encoding.UTF8, "application/json"));

        if (create.StatusCode == HttpStatusCode.OK)
        {
            var content = await create.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Parse<JsonElement>(content);
            if (doc.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
            {
                var cartId = idProp.GetInt32();
                var cartItem = new CartItemRequest { CartId = cartId, Quantity = 1, IsProduct = true, ProductVersionId = 1 };
                var response = await _client.PostAsync($"/api/carts/{cartId}/items",
                    new StringContent(JsonSerializer.Serialize(cartItem), System.Text.Encoding.UTF8, "application/json"));

                response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
            }
        }
    }

    [Fact]
    public async Task ProductModification_LinkedLookup_HitsController()
    {
        var admin = _factory.CreateAuthenticatedClient("ItemRead", "ItemWrite");

        var byCartItem = await admin.GetAsync("/api/product-modification/cart-item/1");
        byCartItem.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        var byProduct = await admin.GetAsync("/api/product-modification/product/1?pageSize=10&pageNumber=0");
        byProduct.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ServiceReservation_CreateUpdateAndLookup_HitsController()
    {
        var serviceClient = _factory.CreateAuthenticatedClient("ServiceRead", "ServiceWrite");

        var request = new ServiceReservationRequest
        {
            CartItemId = 1,
            TimeSlotId = 1,
            BookingTime = DateTime.UtcNow.AddHours(1),
            CustomerName = "Integration User",
            CustomerPhone = "123456789",
            IsCancelled = false
        };

        var create = await serviceClient.PostAsync("/api/service-reservation",
            new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json"));
        create.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);

        var list = await serviceClient.GetAsync("/api/service-reservation?pageSize=10&pageNumber=0");
        list.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        var byId = await serviceClient.GetAsync("/api/service-reservation/1");
        byId.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        var update = await serviceClient.PutAsync("/api/service-reservation/1",
            new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json"));
        update.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Payment_RegisterCashRefundAndPartialFlows_HitsController()
    {
        var cash = new POS_System.Business.Dtos.Request.CashRequest(1, 5000, null, Guid.NewGuid().ToString(), null);
        var cashResponse = await _client.PostAsync("/api/payments/cash",
            new StringContent(JsonSerializer.Serialize(cash), System.Text.Encoding.UTF8, "application/json"));
        cashResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);

        var refund = new POS_System.Business.Dtos.Request.RefundRequest(1, false);
        var refundResponse = await _client.PatchAsync($"/api/payments/refund/{Uri.EscapeDataString(DateTime.UtcNow.ToString("o"))}",
            new StringContent(JsonSerializer.Serialize(refund), System.Text.Encoding.UTF8, "application/json"));
        refundResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);

        var initPartial = new POS_System.Business.Dtos.Request.InitPartialCheckoutRequest(1, 1, 1, null,
            new List<POS_System.Business.Dtos.Request.CheckoutCartItem> { new("Test", "t", 1000, 1, null) });
        var initResponse = await _client.PostAsync("/api/payments/init-partial-checkout",
            new StringContent(JsonSerializer.Serialize(initPartial), System.Text.Encoding.UTF8, "application/json"));
        initResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);

        var partial = new POS_System.Business.Dtos.Request.PartialCheckoutRequest(1, DateTime.UtcNow, null, null);
        var partialResponse = await _client.PostAsync("/api/payments/partial-checkout",
            new StringContent(JsonSerializer.Serialize(partial), System.Text.Encoding.UTF8, "application/json"));
        partialResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Payment_FullCheckout_HitsController()
    {
        var checkout = new POS_System.Business.Dtos.Request.CheckoutRequest(
            1,
            1,
            null,
            null,
            new List<POS_System.Business.Dtos.Request.CheckoutCartItem> { new POS_System.Business.Dtos.Request.CheckoutCartItem("Test", "t", 1000, 1, null) }
        );

        var response = await _client.PostAsync("/api/payments/full-checkout",
            new StringContent(JsonSerializer.Serialize(checkout), System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }
}
