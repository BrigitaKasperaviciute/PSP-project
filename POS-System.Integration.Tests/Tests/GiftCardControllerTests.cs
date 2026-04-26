using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class GiftCardControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithAllClaims();
    private readonly HttpClient _anonClient = factory.CreateClient();

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/giftcards");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/giftcards");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ValidGiftCard_ReturnsOk()
    {
        var request = new { Date = "2027-12-31", Value = 5000 };

        var response = await _client.PostAsJsonAsync("/api/giftcards", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_ExistingGiftCard_ReturnsOk()
    {
        var createRequest = new { Date = "2027-06-30", Value = 2500 };
        var createResponse = await _client.PostAsJsonAsync("/api/giftcards", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<GiftCardResponse>();

        var response = await _client.GetAsync($"/api/giftcards/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistingGiftCard_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/giftcards/nonexistent-id-xyz");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ExistingGiftCard_ReturnsOk()
    {
        var createRequest = new { Date = "2027-01-01", Value = 1000 };
        var createResponse = await _client.PostAsJsonAsync("/api/giftcards", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<GiftCardResponse>();

        var updateRequest = new { Date = "2028-01-01", Value = 1500 };
        var response = await _client.PutAsJsonAsync($"/api/giftcards/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_ExistingGiftCard_ReturnsNoContent()
    {
        var createRequest = new { Date = "2027-03-15", Value = 3000 };
        var createResponse = await _client.PostAsJsonAsync("/api/giftcards", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<GiftCardResponse>();

        var response = await _client.DeleteAsync($"/api/giftcards/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
