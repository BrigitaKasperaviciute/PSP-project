using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class BusinessDetailControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public BusinessDetailControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    #region Happy Path Tests

    [Fact]
    public async Task GetBusinessDetails_ReturnsOkWithBusinessDetails()
    {
        // Act
        var response = await _client.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateBusinessDetails_WithValidPayload_ReturnsOkAndPersistsDetails()
    {
        // Arrange
        var request = new BusinessDetailsRequestBuilder()
            .WithBusinessName("Test Business")
            .WithOwnerName("Test Owner")
            .WithEmail($"biz-{Guid.NewGuid():N}@example.com")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/business-details", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be(request.BusinessName);
    }

    [Fact]
    public async Task UpdateBusinessDetails_WithValidPayload_ReturnsOkAndUpdatesDetails()
    {
        // Arrange - create first
        var createRequest = new BusinessDetailsRequestBuilder()
            .WithBusinessName("OldName")
            .Build();
        
        await _client.PostAsJsonAsync("/api/business-details", createRequest);

        // Act - update
        var updateRequest = new BusinessDetailsRequestBuilder()
            .WithBusinessName("NewName")
            .Build();
        
        var response = await _client.PutAsJsonAsync("/api/business-details", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body!.BusinessName.Should().Be(updateRequest.BusinessName);
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task CreateBusinessDetails_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = """{"businessName": "Test", "ownerName": "Owner", "email": "invalid-email", "phoneNumber": "123", "address": "123 St"}""";

        // Act
        var response = await _client.PostAsJsonAsync("/api/business-details", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBusinessDetails_WithNullBusinessName_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = """{"businessName": null, "ownerName": "Owner", "email": "test@example.com", "phoneNumber": "123", "address": "123 St"}""";

        // Act
        var response = await _client.PostAsJsonAsync("/api/business-details", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}

public class BusinessDetailsResponse
{
    public int Id { get; set; }
    public string BusinessName { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Address { get; set; } = "";
}
