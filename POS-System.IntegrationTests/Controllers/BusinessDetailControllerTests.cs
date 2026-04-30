using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class BusinessDetailControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private string BusinessDetailsFilePath => Path.Combine(_factory.TempDirectory, "business-details.json");

    public BusinessDetailControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public Task InitializeAsync()
    {
        if (File.Exists(BusinessDetailsFilePath))
            File.Delete(BusinessDetailsFilePath);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static BusinessDetailsRequest MakeRequest() => new()
    {
        BusinessName = "Test Corp",
        BusinessEmail = "contact@testcorp.com",
        BusinessPhone = "+1234567890",
        Country = "Lithuania",
        City = "Vilnius",
        Street = "Main St",
        HouseNumber = 10,
        FlatNumber = null
    };

    // ── GET ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBusinessDetails_WhenFileExists_ReturnsOkWithDetails()
    {
        // Arrange – write a valid JSON file so the repository can read it
        var content = """{"BusinessName":"Test Corp","BusinessEmail":"contact@testcorp.com","BusinessPhone":"+1234567890","Country":"Lithuania","City":"Vilnius","Street":"Main St","HouseNumber":10,"FlatNumber":null}""";
        await File.WriteAllTextAsync(BusinessDetailsFilePath, content);

        var client = _factory.CreateClientWithClaims("BusinessDetailsRead");

        // Act
        var response = await client.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>(_jsonOptions);
        body!.BusinessName.Should().Be("Test Corp");
        body.City.Should().Be("Vilnius");
    }

    [Fact]
    public async Task GetBusinessDetails_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBusinessDetails_WhenFileDoesNotExist_ReturnsInternalServerError()
    {
        // Arrange – InitializeAsync ensures no file exists
        var client = _factory.CreateClientWithClaims("BusinessDetailsRead");

        // Act
        var response = await client.GetAsync("/api/business-details");

        // Assert
        // BusinessDetailRepository throws FileNotFoundException when the file is absent;
        // the global exception handler maps unhandled exceptions to 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── CREATE / UPDATE ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBusinessDetails_WithValidRequest_ReturnsOkAndPersistsData()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("BusinessDetailsWrite");
        var request = MakeRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/business-details", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>(_jsonOptions);
        body!.BusinessName.Should().Be("Test Corp");
        body.Country.Should().Be("Lithuania");

        // Verify the file was written
        File.Exists(BusinessDetailsFilePath).Should().BeTrue();
        var fileContent = await File.ReadAllTextAsync(BusinessDetailsFilePath);
        fileContent.Should().Contain("Test Corp");
    }

    [Fact]
    public async Task CreateBusinessDetails_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/business-details", MakeRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateBusinessDetails_WithValidRequest_ReturnsOkWithUpdatedData()
    {
        // Arrange – create initial data first
        var setupContent = """{"BusinessName":"Old Corp","BusinessEmail":"old@corp.com","BusinessPhone":"000","Country":"Estonia","City":"Tallinn","Street":"Old St","HouseNumber":1,"FlatNumber":null}""";
        await File.WriteAllTextAsync(BusinessDetailsFilePath, setupContent);

        var client = _factory.CreateClientWithClaims("BusinessDetailsWrite");
        var request = MakeRequest() with { BusinessName = "New Corp", City = "Kaunas" };

        // Act
        var response = await client.PutAsJsonAsync("/api/business-details", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>(_jsonOptions);
        body!.BusinessName.Should().Be("New Corp");
        body.City.Should().Be("Kaunas");

        // Verify the file was replaced
        var fileContent = await File.ReadAllTextAsync(BusinessDetailsFilePath);
        fileContent.Should().Contain("New Corp");
        fileContent.Should().NotContain("Old Corp");
    }

    [Fact]
    public async Task UpdateBusinessDetails_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PutAsJsonAsync("/api/business-details", MakeRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
