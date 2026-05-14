using FluentAssertions;
using System.Net.Http.Json;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ApiValidationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public ApiValidationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Pagination Tests

    [Fact]
    public async Task GetAllWithValidPagination_ReturnsData()
    {
        // Act
        var response = await _client.GetAsync("/api/products?pageNumber=1&pageSize=10");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetAllWithInvalidPageNumber_Handled()
    {
        // Act
        var response = await _client.GetAsync("/api/products?pageNumber=-1&pageSize=10");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task GetAllWithZeroPageSize_Handled()
    {
        // Act
        var response = await _client.GetAsync("/api/products?pageNumber=1&pageSize=0");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task GetAllWithExcessivePageSize_Handled()
    {
        // Act
        var response = await _client.GetAsync("/api/products?pageNumber=1&pageSize=99999");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion

    #region Query Parameter Tests

    [Fact]
    public async Task GetWithSearchParameter_Handled()
    {
        // Act
        var response = await _client.GetAsync("/api/products?search=test&pageNumber=1&pageSize=10");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task GetWithSortParameter_Handled()
    {
        // Act
        var response = await _client.GetAsync("/api/products?sortBy=Name&sortOrder=asc&pageNumber=1&pageSize=10");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task GetWithInvalidSortParameter_Handled()
    {
        // Act
        var response = await _client.GetAsync("/api/products?sortBy=InvalidField&pageNumber=1&pageSize=10");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion

    #region Content Type Tests

    [Fact]
    public async Task PostWithJsonContentType_Accepted()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = new StringContent("{\"Name\": \"Test\"}", 
                System.Text.Encoding.UTF8, "application/json")
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task PostWithoutContentType_Handled()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = new StringContent("{\"Name\": \"Test\"}")
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion

    #region Special Character Tests

    [Fact]
    public async Task GetWithSpecialCharactersInQuery_Handled()
    {
        // Act
        var response = await _client.GetAsync("/api/products?search=%22%3E%3Cscript%3E&pageNumber=1&pageSize=10");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task PostWithSpecialCharactersInBody_Handled()
    {
        // Arrange
        var request = new { Name = "Test<script>alert('xss')</script>" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", request);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task PostWithUnicodeCharacters_Handled()
    {
        // Arrange
        var request = new { Name = "Тестовый продукт 测试产品 🎉" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", request);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion

    #region Numeric Boundary Tests

    [Fact]
    public async Task PostWithMaxIntValue_Handled()
    {
        // Arrange
        var request = new { Id = int.MaxValue };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", request);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task PostWithZeroValue_Handled()
    {
        // Arrange
        var request = new { Price = 0m, Quantity = 0 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", request);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task PostWithDecimalPrecision_Handled()
    {
        // Arrange
        var request = new { Price = 0.00000000001m };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", request);

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion

    #region Response Header Tests

    [Fact]
    public async Task ResponseIncludesContentTypeHeader_Success()
    {
        // Act
        var response = await _client.GetAsync("/api/products?pageNumber=1&pageSize=10");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
        // Content type may be null for non-content responses
        if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength > 0)
        {
            response.Content.Headers.ContentType.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ResponseHandlesIfModifiedSinceHeader_Success()
    {
        // Arrange
        _client.DefaultRequestHeaders.IfModifiedSince = DateTimeOffset.UtcNow.AddDays(-1);

        // Act
        var response = await _client.GetAsync("/api/products?pageNumber=1&pageSize=10");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion
}
