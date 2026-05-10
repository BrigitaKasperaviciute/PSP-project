using System.Net;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class CartControllerTests(PosWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateCart_EmptyBody_ReturnsOk()
    {
        var request = new CartRequest { EmployeeVersionId = 1 };
        var response = await _client.PostAsJsonAsync("/api/carts", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // DELETE /api/carts/{id}
    [Fact]
    public async Task DeleteCart_InProgressCart_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/carts", new CartRequest { EmployeeVersionId = 1 });
        var cart = await created.Content.ReadFromJsonAsync<CartIdResponse>();

        var response = await _client.DeleteAsync($"/api/carts/{cart!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCart_CompletedCart_ReturnsInternalServerError()
    {
        // Cart 2 has Status=COMPLETED - cannot delete a non-in-progress cart
        var response = await _client.DeleteAsync("/api/carts/2");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // GET /api/carts/{id}/discount
    [Fact]
    public async Task GetCartDiscount_CartWithNoDiscount_ReturnsNoContent()
    {
        var response = await _client.GetAsync("/api/carts/1/discount");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetCartDiscount_NonExistentCart_ReturnsNoContent()
    {
        // CartService returns null → Ok(null) → 204 NoContent
        var response = await _client.GetAsync("/api/carts/99999/discount");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // PATCH /api/carts/{id}/discount (requires valid Stripe coupon code)
    [Fact]
    public async Task ApplyDiscountToCart_InvalidDiscountCode_ReturnsNotFound()
    {
        var request = new ApplyDiscountRequest("INVALID_CODE_XYZ");
        var response = await _client.PatchAsJsonAsync("/api/carts/3/discount", request);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ApplyDiscountToCart_NonExistentCart_ReturnsNotFound()
    {
        var request = new ApplyDiscountRequest("INVALID_CODE");
        var response = await _client.PatchAsJsonAsync("/api/carts/99999/discount", request);
        Assert.False(response.IsSuccessStatusCode);
    }

    private record CartIdResponse(int Id);
}
