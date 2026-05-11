using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartDiscountControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public CartDiscountControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateCartDiscount_WithValidPayload_ReturnsOk()
    {
        // Arrange
        var request = new CartDiscountRequestBuilder()
            .WithValue(15)
            .WithIsPercentage(true)
            .WithEndDate(DateTime.UtcNow.AddDays(7))
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCartDiscount_WithPercentageDiscount_ReturnsOk()
    {
        // Arrange
        var request = new CartDiscountRequestBuilder()
            .WithValue(25)
            .WithIsPercentage(true)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateCartDiscount_WithFixedAmountDiscount_ReturnsOk()
    {
        // Arrange
        var request = new CartDiscountRequestBuilder()
            .WithValue(5000)
            .WithIsPercentage(false)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCartDiscountById_WithExistingId_ReturnsOk()
    {
        // Arrange
        var createRequest = new CartDiscountRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/cart-discount", createRequest);
        var createdDiscount = await createResponse.Content.ReadFromJsonAsync<CartDiscountResponse>();

        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{createdDiscount!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCartDiscountById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/cart-discount/non-existent-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCartDiscountById_WithExistingId_ReturnsOk()
    {
        // Arrange
        var createRequest = new CartDiscountRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/cart-discount", createRequest);
        var createdDiscount = await createResponse.Content.ReadFromJsonAsync<CartDiscountResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/cart-discount/{createdDiscount!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateCartDiscount_MultipleVariations_AllSucceed()
    {
        // Arrange
        var variations = new[]
        {
            new CartDiscountRequestBuilder().WithValue(10).WithIsPercentage(true).Build(),
            new CartDiscountRequestBuilder().WithValue(20).WithIsPercentage(false).Build(),
            new CartDiscountRequestBuilder().WithValue(0).WithIsPercentage(true).Build()
        };

        // Act & Assert
        foreach (var request in variations)
        {
            var response = await _client.PostAsJsonAsync("/api/cart-discount", request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    #endregion
}
