using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class BusinessDetailControllerTests(PosWebApplicationFactory factory)
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

    private static BusinessDetailsRequest ValidRequest() => new()
    {
        BusinessName = "IntTest Business",
        BusinessEmail = "business@test.com",
        BusinessPhone = "1234567890",
        Country = "Lithuania",
        City = "Vilnius",
        Street = "Test Street",
        HouseNumber = 10,
        FlatNumber = null
    };

    // POST /api/business-details
    [Fact]
    public async Task CreateBusinessDetails_ValidRequest_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/business-details", ValidRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateBusinessDetails_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.PostAsJsonAsync("/api/business-details", ValidRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // GET /api/business-details
    [Fact]
    public async Task GetBusinessDetails_AfterCreate_ReturnsOk()
    {
        await _client.PostAsJsonAsync("/api/business-details", ValidRequest());

        var response = await _client.GetAsync("/api/business-details");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBusinessDetails_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/business-details");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // PUT /api/business-details
    [Fact]
    public async Task UpdateBusinessDetails_ValidRequest_ReturnsOk()
    {
        await _client.PostAsJsonAsync("/api/business-details", ValidRequest());

        var updateReq = ValidRequest() with { BusinessName = "Updated Business" };
        var response = await _client.PutAsJsonAsync("/api/business-details", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBusinessDetails_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.PutAsJsonAsync("/api/business-details", ValidRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
