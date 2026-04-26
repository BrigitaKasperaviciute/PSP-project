using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class BusinessDetailControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithAllClaims();
    private readonly HttpClient _anonClient = factory.CreateClient();

    [Fact]
    public async Task GetBusinessDetails_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/business-details");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBusinessDetails_ValidRequest_ReturnsOk()
    {
        var request = new
        {
            BusinessName = "Test POS Business",
            BusinessEmail = "business@test.com",
            BusinessPhone = "123456789",
            Country = "Lithuania",
            City = "Vilnius",
            Street = "Test Street",
            HouseNumber = 2,
            FlatNumber = (int?)null
        };

        var response = await _client.PostAsJsonAsync("/api/business-details", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateBusinessDetails_ValidRequest_ReturnsOk()
    {
        var request = new
        {
            BusinessName = "Updated POS Business",
            BusinessEmail = "updated@test.com",
            BusinessPhone = "987654321",
            Country = "Lithuania",
            City = "Kaunas",
            Street = "Updated Street",
            HouseNumber = 5,
            FlatNumber = 2
        };

        var response = await _client.PutAsJsonAsync("/api/business-details", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBusinessDetails_AfterCreate_ReturnsOk()
    {
        var createRequest = new
        {
            BusinessName = "Get After Create",
            BusinessEmail = "get@test.com",
            BusinessPhone = "111000111",
            Country = "Lithuania",
            City = "Vilnius",
            Street = "Main St",
            HouseNumber = 10,
            FlatNumber = (int?)null
        };
        await _client.PostAsJsonAsync("/api/business-details", createRequest);

        var response = await _client.GetAsync("/api/business-details");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
