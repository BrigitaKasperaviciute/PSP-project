using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class ItemDiscountControllerTests(PosWebApplicationFactory factory)
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

    // GET /api/item-discount
    [Fact]
    public async Task GetAllItemDiscounts_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/item-discount");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllItemDiscounts_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/item-discount");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // POST /api/item-discount
    [Fact]
    public async Task CreateItemDiscount_ValidRequest_ReturnsOk()
    {
        var request = new ItemDiscountRequest
        {
            Value = 15,
            IsPercentage = true,
            Description = "IntTest Discount",
            StartDate = null,
            EndDate = null
        };
        var response = await _client.PostAsJsonAsync("/api/item-discount", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateItemDiscount_NoToken_ReturnsUnauthorized()
    {
        var request = new ItemDiscountRequest { Value = 10, IsPercentage = true, Description = "D", StartDate = null, EndDate = null };
        var response = await _anonClient.PostAsJsonAsync("/api/item-discount", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // GET /api/item-discount/{id}
    [Fact]
    public async Task GetItemDiscountById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/item-discount/2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetItemDiscountById_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/item-discount/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/item-discount/{id}
    [Fact]
    public async Task DeleteItemDiscount_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/item-discount",
            new ItemDiscountRequest { Value = 5, IsPercentage = true, Description = "ToDelete", StartDate = null, EndDate = null });
        var discount = await created.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.DeleteAsync($"/api/item-discount/{discount!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteItemDiscount_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/item-discount/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PUT /api/item-discount/{id}
    [Fact]
    public async Task UpdateItemDiscount_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/item-discount",
            new ItemDiscountRequest { Value = 10, IsPercentage = true, Description = "ToUpdate", StartDate = null, EndDate = null });
        var discount = await created.Content.ReadFromJsonAsync<IdResponse>();

        var updateReq = new ItemDiscountRequest { Value = 20, IsPercentage = true, Description = "Updated", StartDate = null, EndDate = null };
        var response = await _client.PutAsJsonAsync($"/api/item-discount/{discount!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateItemDiscount_NonExistentId_ReturnsNotFound()
    {
        var updateReq = new ItemDiscountRequest { Value = 5, IsPercentage = true, Description = "X", StartDate = null, EndDate = null };
        var response = await _client.PutAsJsonAsync("/api/item-discount/99999", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PUT /api/item-discount/{id}/link
    [Fact]
    public async Task LinkItemDiscountToItems_ValidIds_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/item-discount/2/link?itemsAreProducts=true", new[] { 4 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LinkItemDiscountToItems_NonExistentDiscount_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/item-discount/99999/link?itemsAreProducts=true", new[] { 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // PUT /api/item-discount/{id}/unlink
    [Fact]
    public async Task UnlinkItemDiscountFromItems_ValidIds_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/item-discount/2/unlink?itemsAreProducts=true", new[] { 4 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkItemDiscountFromItems_NonExistentDiscount_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/item-discount/99999/unlink?itemsAreProducts=true", new[] { 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // GET /api/item-discount/item/{id}
    [Fact]
    public async Task GetItemDiscountsLinkedToItemId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/item-discount/item/4?isProduct=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetItemDiscountsLinkedToItemId_NonExistentItem_ReturnsOkEmptyList()
    {
        var response = await _client.GetAsync("/api/item-discount/item/99999?isProduct=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private record IdResponse(int Id);
}
