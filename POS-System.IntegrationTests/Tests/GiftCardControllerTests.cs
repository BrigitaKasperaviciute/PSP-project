using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class GiftCardControllerTests(PosWebApplicationFactory factory)
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

    // GET /api/giftcards
    [Fact]
    public async Task GetAllGiftCards_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/giftcards");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllGiftCards_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/giftcards");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // POST /api/giftcards
    [Fact]
    public async Task CreateGiftCard_ValidRequest_ReturnsOk()
    {
        var request = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            Value = 5000
        };
        var response = await _client.PostAsJsonAsync("/api/giftcards", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateGiftCard_ExpiredDate_ReturnsBadRequest()
    {
        var request = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Value = 5000
        };
        var response = await _client.PostAsJsonAsync("/api/giftcards", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // GET /api/giftcards/{id}
    [Fact]
    public async Task GetGiftCardById_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/giftcards",
            new GiftCardRequest { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Value = 1000 });
        var gc = await created.Content.ReadFromJsonAsync<GiftCardIdResponse>();

        var response = await _client.GetAsync($"/api/giftcards/{gc!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetGiftCardById_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/giftcards/nonexistent_code_xyz");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PUT /api/giftcards/{id}
    [Fact]
    public async Task UpdateGiftCard_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/giftcards",
            new GiftCardRequest { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Value = 2000 });
        var gc = await created.Content.ReadFromJsonAsync<GiftCardIdResponse>();

        var updateReq = new GiftCardRequest { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)), Value = 3000 };
        var response = await _client.PutAsJsonAsync($"/api/giftcards/{gc!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateGiftCard_NonExistentId_ReturnsNotFound()
    {
        var updateReq = new GiftCardRequest { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Value = 100 };
        var response = await _client.PutAsJsonAsync("/api/giftcards/nonexistent_xyz", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/giftcards/{id}
    [Fact]
    public async Task DeleteGiftCard_ExistingId_ReturnsNoContent()
    {
        var created = await _client.PostAsJsonAsync("/api/giftcards",
            new GiftCardRequest { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Value = 1500 });
        var gc = await created.Content.ReadFromJsonAsync<GiftCardIdResponse>();

        var response = await _client.DeleteAsync($"/api/giftcards/{gc!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteGiftCard_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/giftcards/nonexistent_xyz");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record GiftCardIdResponse(int Id);
}
