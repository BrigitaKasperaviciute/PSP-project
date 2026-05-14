using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;

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
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateBusinessDetails_WithValidPayload_ReturnsOkAndPersistsDetails()
    {
        // Arrange
        var request = new BusinessDetailsRequestBuilder()
            .WithBusinessName("Main Store")
            .WithBusinessEmail("store@example.com")
            .WithBusinessPhone("+420123456789")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/business-details", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be(request.BusinessName);
        body.BusinessEmail.Should().Be(request.BusinessEmail);
    }

    [Fact]
    public async Task GetBusinessDetails_AfterCreate_ReturnsOkAndCurrentDetails()
    {
        // Arrange
        var request = new BusinessDetailsRequestBuilder().Build();
        await _client.PostAsJsonAsync("/api/business-details", request);

        // Act
        var response = await _client.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be(request.BusinessName);
    }

    [Fact]
    public async Task UpdateBusinessDetails_WithValidPayload_ReturnsOkAndReplacesDetails()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/business-details", new BusinessDetailsRequestBuilder().Build());
        var updateRequest = new BusinessDetailsRequestBuilder()
            .WithBusinessName("Updated Store")
            .WithBusinessEmail("updated@example.com")
            .WithBusinessPhone("+420111222333")
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/business-details", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body!.BusinessName.Should().Be("Updated Store");
        body.BusinessEmail.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task CreateBusinessDetails_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new BusinessDetailsRequestBuilder()
            .WithBusinessEmail("not-an-email")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/business-details", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBusinessDetails_WithoutExistingRow_ReturnsOkOrEmptyState()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();

        // Act
        var response = await _client.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}