using System.Net;
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

    private const string FilePath = "./business-details.json";

    public BusinessDetailControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClientWithClaims("BusinessDetailsRead", "BusinessDetailsWrite");
    }

    public Task InitializeAsync()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static BusinessDetailsRequest ValidRequest() => new()
    {
        BusinessName = "Test Shop",
        BusinessEmail = "shop@test.com",
        BusinessPhone = "+37060000000",
        Country = "Lithuania",
        City = "Vilnius",
        Street = "Main St",
        HouseNumber = 2,
        FlatNumber = null
    };

    // --- Create (POST) ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/business-details", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be("Test Shop");
        body.City.Should().Be("Vilnius");

        File.Exists(FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync("/api/business-details", ValidRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        var readClient = _factory.CreateClientWithClaims("BusinessDetailsRead");
        var response = await readClient.PostAsJsonAsync("/api/business-details", ValidRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Get ---

    [Fact]
    public async Task Get_AfterCreate_ReturnsOkAndCorrectDetails()
    {
        await _client.PostAsJsonAsync("/api/business-details", ValidRequest());

        var response = await _client.GetAsync("/api/business-details");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be("Test Shop");
    }

    [Fact]
    public async Task Get_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/business-details");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- Update (PUT) ---

    [Fact]
    public async Task Update_AfterCreate_ReturnsOkWithUpdatedValues()
    {
        await _client.PostAsJsonAsync("/api/business-details", ValidRequest());

        var updated = ValidRequest() with { BusinessName = "Updated Shop", City = "Kaunas" };
        var response = await _client.PutAsJsonAsync("/api/business-details", updated);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body!.BusinessName.Should().Be("Updated Shop");
        body.City.Should().Be("Kaunas");
    }

    [Fact]
    public async Task Update_WithoutWriteClaim_Returns403()
    {
        var readClient = _factory.CreateClientWithClaims("BusinessDetailsRead");
        var response = await readClient.PutAsJsonAsync("/api/business-details", ValidRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
