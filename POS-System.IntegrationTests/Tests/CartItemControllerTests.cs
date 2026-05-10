using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class CartItemControllerTests(PosWebApplicationFactory factory)
{
    private readonly HttpClient _client = CreateAuthClient(factory);
    private readonly HttpClient _anonClient = factory.CreateClient();

    private static HttpClient CreateAuthClient(PosWebApplicationFactory f)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.FullAccessToken);
        return c;
    }

    // GET /api/carts/{cartid}/items
    [Fact]
    public async Task GetAllCartItems_ExistingCart_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/carts/1/items");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllCartItems_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/carts/1/items");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // GET /api/carts/{cartid}/items/{id}
    [Fact]
    public async Task GetCartItemById_ExistingItem_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/carts/1/items/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCartItemById_NonExistentItem_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/carts/1/items/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // POST /api/carts/{cartid}/items
    [Fact]
    public async Task CreateCartItem_ValidRequest_ReturnsOk()
    {
        var request = new CartItemRequest
        {
            CartId = 3,
            Quantity = 1,
            IsProduct = true,
            ProductVersionId = 4
        };
        var response = await _client.PostAsJsonAsync("/api/carts/3/items", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateCartItem_NoToken_ReturnsUnauthorized()
    {
        var request = new CartItemRequest { CartId = 1, Quantity = 1, IsProduct = true, ProductVersionId = 1 };
        var response = await _anonClient.PostAsJsonAsync("/api/carts/1/items", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // PUT /api/carts/{cartid}/items/{id}
    [Fact]
    public async Task UpdateCartItem_ExistingItem_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/carts/3/items",
            new CartItemRequest { CartId = 3, Quantity = 1, IsProduct = true, ProductVersionId = 4 });
        var item = await created.Content.ReadFromJsonAsync<CartItemIdResponse>();

        var updateReq = new CartItemRequest { CartId = 3, Quantity = 2, IsProduct = true, ProductVersionId = 4 };
        var response = await _client.PutAsJsonAsync($"/api/carts/3/items/{item!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCartItem_NonExistentItem_ReturnsNotFound()
    {
        var updateReq = new CartItemRequest { CartId = 1, Quantity = 1, IsProduct = true, ProductVersionId = 1 };
        var response = await _client.PutAsJsonAsync("/api/carts/1/items/99999", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/carts/{cartid}/items/{id}
    [Fact]
    public async Task DeleteCartItem_ExistingItem_ReturnsNoContent()
    {
        var created = await _client.PostAsJsonAsync("/api/carts/3/items",
            new CartItemRequest { CartId = 3, Quantity = 1, IsProduct = true, ProductVersionId = 4 });
        var item = await created.Content.ReadFromJsonAsync<CartItemIdResponse>();

        var response = await _client.DeleteAsync($"/api/carts/3/items/{item!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCartItem_NonExistentItem_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/carts/1/items/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PUT /api/carts/{cartid}/items/{id}/link
    [Fact]
    public async Task LinkCartItemToProductModifications_ValidIds_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/carts/1/items/1/link", new[] { 2 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LinkCartItemToProductModifications_NonExistentItem_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/carts/1/items/99999/link", new[] { 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // PUT /api/carts/{cartid}/items/{id}/unlink
    [Fact]
    public async Task UnlinkCartItemFromProductModifications_ValidIds_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/carts/1/items/1/unlink", new[] { 2 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkCartItemFromProductModifications_NonExistentItem_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/carts/1/items/99999/unlink", new[] { 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private record CartItemIdResponse(int Id);
}
