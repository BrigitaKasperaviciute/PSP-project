using System.Net;
using Xunit;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class BusinessDetailControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public BusinessDetailControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public Task InitializeAsync()
    {
        // BusinessDetails is file-based; delete the test file before each test for isolation
        var filePath = Path.Combine(Path.GetTempPath(), "pos-test-business-details.json");
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetBusinessDetails ---------------

    [Fact]
    public async Task GetBusinessDetails_WhenDetailsExist_ReturnsOkWithDetails()
    {
        // Arrange – seed one business details record via POST
        await _client.PostAsJsonAsync("/api/business-details", BuildRequest("Test Business"));

        // Act
        var response = await _client.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be("Test Business");
        body.Country.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetBusinessDetails_WhenNoDetailsExist_ReturnsInternalServerError()
    {
        // Arrange – InitializeAsync already deleted the file

        // Act
        var response = await _client.GetAsync("/api/business-details");

        // Assert – FileNotFoundException is not a BaseException so it maps to 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // --------------- CreateBusinessDetails ---------------

    [Fact]
    public async Task CreateBusinessDetails_WithValidRequest_ReturnsOkAndPersists()
    {
        // Arrange
        var request = BuildRequest("My Shop");

        // Act
        var response = await _client.PostAsJsonAsync("/api/business-details", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be(request.BusinessName);
        body.BusinessEmail.Should().Be(request.BusinessEmail);
        body.City.Should().Be(request.City);

        // Assert – persisted by reading back via GET
        var getResponse = await _client.GetAsync("/api/business-details");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var persisted = await getResponse.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        persisted!.BusinessName.Should().Be(request.BusinessName);
    }

    [Fact]
    public async Task CreateBusinessDetails_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();
        var request = BuildRequest("Unauth Shop");

        // Act
        var response = await unauthClient.PostAsJsonAsync("/api/business-details", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBusinessDetails_WithMissingName_ReturnsBadRequest()
    {
        // Arrange – omit BusinessName (required)
        var payload = new
        {
            BusinessEmail = "shop@example.com",
            BusinessPhone = "+37061234567",
            Country = "Lithuania",
            City = "Kaunas",
            Street = "Laisves al.",
            HouseNumber = 10
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/business-details", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --------------- UpdateBusinessDetails ---------------

    [Fact]
    public async Task UpdateBusinessDetails_WhenDetailsExist_ReturnsOkAndUpdates()
    {
        // Arrange – create first, then update
        await _client.PostAsJsonAsync("/api/business-details", BuildRequest("Original Name"));
        var updateRequest = BuildRequest("Updated Name") with { City = "Vilnius" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/business-details", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be("Updated Name");
        body.City.Should().Be("Vilnius");

        // Assert – persisted by reading back via GET
        var getResponse = await _client.GetAsync("/api/business-details");
        var persisted = await getResponse.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        persisted!.BusinessName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateBusinessDetails_WithMissingEmail_ReturnsBadRequest()
    {
        // Arrange – omit required BusinessEmail
        var payload = new
        {
            BusinessName = "Shop",
            BusinessPhone = "+37061234567",
            Country = "Lithuania",
            City = "Kaunas",
            Street = "Laisves al.",
            HouseNumber = 10
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/business-details", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- helpers ----

    private static BusinessDetailsRequest BuildRequest(string name) => new()
    {
        BusinessName = name,
        BusinessEmail = "shop@example.com",
        BusinessPhone = "+37061234567",
        Country = "Lithuania",
        City = "Kaunas",
        Street = "Laisves al.",
        HouseNumber = 10,
        FlatNumber = null
    };
}
