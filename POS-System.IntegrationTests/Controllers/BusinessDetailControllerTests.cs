using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

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
        _client = factory.CreateClientWithClaims("BusinessDetailsRead", "BusinessDetailsWrite");
    }

    // BusinessDetails are stored as a JSON file (not in the database).
    // Each test that reads first ensures the file exists by writing via POST.
    // InitializeAsync deletes the temp file so GET-before-POST tests start clean.
    public Task InitializeAsync()
    {
        var fullPath = Path.Combine(Path.GetTempPath(), "business_details_test.json");
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static BusinessDetailsRequest BuildRequest(string suffix = "") => new()
    {
        BusinessName = $"Test Business{suffix}",
        BusinessEmail = $"biz{suffix}@test.com",
        BusinessPhone = "37060000000",
        Country = "Lithuania",
        City = "Vilnius",
        Street = "Gedimino pr.",
        HouseNumber = 1,
        FlatNumber = null
    };

    // --- Get ---

    [Fact]
    public async Task Get_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AfterPost_ReturnsOkWithPersistedDetails()
    {
        // Arrange – create the details first
        await _client.PostAsJsonAsync("/api/business-details", BuildRequest());

        // Act
        var response = await _client.GetAsync("/api/business-details");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be("Test Business");
        body.Country.Should().Be("Lithuania");
        body.City.Should().Be("Vilnius");
    }

    [Fact]
    public async Task Get_WhenNoDetailsExist_Returns500()
    {
        // Arrange – file was deleted in InitializeAsync; no POST done

        // Act
        var response = await _client.GetAsync("/api/business-details");

        // Assert – file is missing; FileNotFoundException propagates as 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // --- Post (create / upsert) ---

    [Fact]
    public async Task Post_WithValidPayload_ReturnsOkAndPersistsDetails()
    {
        // Arrange
        var request = BuildRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/business-details", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be(request.BusinessName);
        body.BusinessEmail.Should().Be(request.BusinessEmail);
        body.Country.Should().Be(request.Country);

        // Assert - file persisted on disk
        var fullPath = Path.Combine(Path.GetTempPath(), "business_details_test.json");
        File.Exists(fullPath).Should().BeTrue();
    }

    [Fact]
    public async Task Post_WithoutWriteClaim_Returns403()
    {
        // Arrange
        var readOnlyClient = _factory.CreateClientWithClaims("BusinessDetailsRead");

        // Act
        var response = await readOnlyClient.PostAsJsonAsync("/api/business-details", BuildRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Put (update) ---

    [Fact]
    public async Task Put_WithValidPayload_ReturnsOkAndUpdatesDetails()
    {
        // Arrange – ensure file exists first
        await _client.PostAsJsonAsync("/api/business-details", BuildRequest());

        // Act
        var response = await _client.PutAsJsonAsync("/api/business-details", BuildRequest(" Updated"));

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body reflects updated values
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be("Test Business Updated");
    }

    [Fact]
    public async Task Put_WithoutWriteClaim_Returns403()
    {
        // Arrange
        var readOnlyClient = _factory.CreateClientWithClaims("BusinessDetailsRead");

        // Act
        var response = await readOnlyClient.PutAsJsonAsync("/api/business-details", BuildRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
