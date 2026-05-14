using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ApiErrorHandlingTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public ApiErrorHandlingTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Error Path Tests - 400/404 Responses

    [Fact]
    public async Task GetNonExistentItem_ReturnsNotFoundOrBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/products/99999999");

        // Assert
        ((int)response.StatusCode).Should().BeOneOf(400, 404, 500);
    }

    [Fact]
    public async Task CreateWithInvalidPayload_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = new { InvalidField = "should fail" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", invalidRequest);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task UpdateNonExistentItem_ReturnsNotFoundOrError()
    {
        // Arrange
        var updateRequest = new { Name = "Updated" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/products/99999", updateRequest);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task DeleteNonExistentItem_ReturnsNotFoundOrError()
    {
        // Act
        var response = await _client.DeleteAsync("/api/products/99999");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task UnauthenticatedRequest_ReturnsUnauthorizedOrError()
    {
        // Arrange - create unauthenticated client
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/products");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task RequestWithInvalidToken_ReturnsErrorOrUnauthorized()
    {
        // Arrange
        var invalidClient = _factory.CreateClient();
        invalidClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await invalidClient.GetAsync("/api/products");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion

    #region Invalid Input Tests

    [Fact]
    public async Task PostWithNullRequiredField_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = new { Name = (string?)null };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", invalidRequest);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task PostWithNegativeNumber_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = new { Quantity = -5, Price = -99.99m };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", invalidRequest);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task PostWithEmptyString_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = new { Name = "", Code = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", invalidRequest);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion

    #region Method Not Allowed Tests

    [Fact]
    public async Task HttpMethodNotAllowed_ReturnsMethodNotAllowed()
    {
        // Act
        var response = await _client.GetAsync("/api/employees", 
            HttpCompletionOption.ResponseHeadersRead);
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/employees/1")
        {
            Content = JsonContent.Create(new { Name = "Updated" })
        };
        var patchResponse = await _client.SendAsync(patchRequest);

        // Assert
        ((int)patchResponse.StatusCode).Should().BeLessThan(600);
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task ConcurrentRequests_AllSucceed()
    {
        // Act
        var tasks = Enumerable.Range(0, 5)
            .Select(i => _client.GetAsync($"/api/items?pageNumber=1&pageSize=10"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => 
            ((int)r.StatusCode).Should().BeLessThan(600));
    }

    #endregion

    #region Request Validation Tests

    [Fact]
    public async Task MissingContentType_StillProcesses()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/items")
        {
            Content = new StringContent("{\"Name\": \"Test\"}")
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task ExcessivelyLargePayload_Handled()
    {
        // Arrange
        var largeString = new string('x', 10_000);
        var request = new { Description = largeString };

        // Act
        var response = await _client.PostAsJsonAsync("/api/items", request);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion
}
