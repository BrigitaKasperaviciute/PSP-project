using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

/// <summary>
/// BusinessDetails are persisted in a JSON file on the file system.
/// Each test starts with no existing file (deleted during ResetDatabaseAsync),
/// so GET before any POST returns a server error (FileNotFoundException).
/// </summary>
public class BusinessDetailControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;
    private readonly HttpClient _writeClient;
    private readonly HttpClient _anonClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BusinessDetailControllerTests(PosSystemApiFactory factory)
    {
        _factory    = factory;
        _readClient  = factory.CreateClientWithClaims("BusinessDetailsRead");
        _writeClient = factory.CreateClientWithClaims("BusinessDetailsRead", "BusinessDetailsWrite");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static BusinessDetailsRequest SampleRequest() => new()
    {
        BusinessName  = "Test Bistro",
        BusinessEmail = "test@bistro.lt",
        BusinessPhone = "+37060000001",
        Country       = "Lithuania",
        City          = "Vilnius",
        Street        = "Gedimino pr.",
        HouseNumber   = 42,
        FlatNumber    = null
    };

    // ── POST /api/business-details ────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsOkWithBusinessDetails()
    {
        // Arrange
        var request = SampleRequest();

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/business-details", request);
        var body = await response.Content.ReadAsStringAsync();
        var details = JsonSerializer.Deserialize<BusinessDetailsResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        details.Should().NotBeNull();
        details!.BusinessName.Should().Be("Test Bistro");
        details.City.Should().Be("Vilnius");
        details.BusinessEmail.Should().Be("test@bistro.lt");
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/business-details", SampleRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/business-details ─────────────────────────────────────────────

    [Fact]
    public async Task Get_AfterCreate_ReturnsOkWithStoredDetails()
    {
        // Arrange — create details first so the JSON file exists
        await _writeClient.PostAsJsonAsync("/api/business-details", SampleRequest());

        // Act
        var response = await _readClient.GetAsync("/api/business-details");
        var body = await response.Content.ReadAsStringAsync();
        var details = JsonSerializer.Deserialize<BusinessDetailsResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        details.Should().NotBeNull();
        details!.BusinessName.Should().Be("Test Bistro");
        details.BusinessEmail.Should().Be("test@bistro.lt");
    }

    [Fact]
    public async Task Get_WithNoFileCreated_ReturnsError()
    {
        // Arrange — no POST before this; the business-details file does not exist

        // Act
        var response = await _readClient.GetAsync("/api/business-details");

        // Assert — FileNotFoundException propagates through GlobalExceptionHandler as a server error
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    // ── PUT /api/business-details ─────────────────────────────────────────────

    [Fact]
    public async Task Update_AfterCreate_ReturnsOkWithUpdatedDetails()
    {
        // Arrange — write initial details, then update them
        await _writeClient.PostAsJsonAsync("/api/business-details", SampleRequest());

        var updatedRequest = SampleRequest() with
        {
            BusinessName = "Updated Bistro",
            City         = "Kaunas"
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/business-details", updatedRequest);
        var body = await response.Content.ReadAsStringAsync();
        var details = JsonSerializer.Deserialize<BusinessDetailsResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        details.Should().NotBeNull();
        details!.BusinessName.Should().Be("Updated Bistro");
        details.City.Should().Be("Kaunas");
    }

    [Fact]
    public async Task Update_WithoutWriteClaim_ReturnsForbidden()
    {
        // Arrange — create details with write client, then attempt update with read-only client
        await _writeClient.PostAsJsonAsync("/api/business-details", SampleRequest());

        // Act
        var response = await _readClient.PutAsJsonAsync("/api/business-details", SampleRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
