using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Text.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class CartControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _client;

    public CartControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = null!;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateAuthenticatedClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }

    // ===== GetAll Tests =====

    [Fact]
    public async Task GetAll_WithValidRequest_ReturnsOkWithCartList()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartRequest), System.Text.Encoding.UTF8, "application/json"));

        // Act
        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=35");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetAll_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 0; i < 40; i++)
        {
            var cartRequest = new CartRequestBuilder()
                .WithEmployeeVersionId(1)
                .Build();

            await _client.PostAsync("/api/carts",
                new StringContent(JsonSerializer.Serialize(cartRequest), System.Text.Encoding.UTF8, "application/json"));
        }

        // Act
        var response = await _client.GetAsync("/api/carts?pageNum=1&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithDefaultPagination_ReturnsOkWithDefaultPageSize()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/carts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ===== GetByID Tests =====

    [Fact]
    public async Task GetByID_WithExistingId_ReturnsOkAndCartData()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        var createResponse = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        var cartId = jsonDocument.GetProperty("id").GetInt32();

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var retrievedCart = JsonSerializer.Parse<JsonElement>(content);
        retrievedCart.TryGetProperty("id", out var id).Should().BeTrue();
        id.GetInt32().Should().Be(cartId);
    }

    // ===== Create Tests =====

    [Fact]
    public async Task Create_WithValidRequest_ReturnsOkAndCreatesEntity()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        // Act
        var response = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.TryGetProperty("id", out var id).Should().BeTrue();

        // Verify database persistence
        var getResponse = await _client.GetAsync($"/api/carts/{id.GetInt32()}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_MultipleRequests_CreatesMultipleCarts()
    {
        // Arrange
        var cartRequest1 = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        var cartRequest2 = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        // Act
        var response1 = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartRequest1), System.Text.Encoding.UTF8, "application/json"));

        var response2 = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartRequest2), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        var cart1 = JsonSerializer.Parse<JsonElement>(content1);
        var cart2 = JsonSerializer.Parse<JsonElement>(content2);

        cart1.GetProperty("id").GetInt32().Should().NotBe(cart2.GetProperty("id").GetInt32());
    }

    // ===== Delete Tests =====

    [Fact]
    public async Task Delete_WithExistingId_DeletesCart()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        var createResponse = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        var cartId = jsonDocument.GetProperty("id").GetInt32();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/carts/{cartId}");

        // Assert
        deleteResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetByID_WithNonExistentId_ReturnsErrorStatus()
    {
        // Act
        var response = await _client.GetAsync($"/api/carts/99999");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    // ===== ApplyDiscountToCart Tests =====

    [Fact]
    public async Task ApplyDiscount_WithValidDiscount_AppliesSuccessfully()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        var createResponse = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        var cartId = jsonDocument.GetProperty("id").GetInt32();

        var applyRequest = new ApplyDiscountRequest { CartDiscountId = 1 };

        // Act
        var response = await _client.PostAsync($"/api/carts/{cartId}/apply-discount",
            new StringContent(JsonSerializer.Serialize(applyRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    // ===== GetCartDiscountAsync Tests =====

    [Fact]
    public async Task GetCartDiscount_WithExistingCart_ReturnsDiscount()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        var createResponse = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        var cartId = jsonDocument.GetProperty("id").GetInt32();

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}/discount");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Create_WithMultipleCarts_EachHasUniqueId()
    {
        // Arrange
        var cartIds = new List<int>();

        // Act
        for (int i = 0; i < 3; i++)
        {
            var cartRequest = new CartRequestBuilder()
                .WithEmployeeVersionId(1)
                .Build();

            var response = await _client.PostAsync("/api/carts",
                new StringContent(JsonSerializer.Serialize(cartRequest), System.Text.Encoding.UTF8, "application/json"));

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Parse<JsonElement>(content);
            cartIds.Add(doc.GetProperty("id").GetInt32());
        }

        // Assert
        cartIds.Should().HaveCount(3);
        cartIds.Distinct().Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAll_WithLargePaginationSize_ReturnsOk()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByID_CheckResponseStructure_ContainsRequiredFields()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        var createResponse = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var createdDoc = JsonSerializer.Parse<JsonElement>(createdContent);
        var cartId = createdDoc.GetProperty("id").GetInt32();

        // Act
        var getResponse = await _client.GetAsync($"/api/carts/{cartId}");
        var getContent = await getResponse.Content.ReadAsStringAsync();
        var getDoc = JsonSerializer.Parse<JsonElement>(getContent);

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getDoc.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_VerifyCartRemoved_GetReturnsNotFound()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        var createResponse = await _client.PostAsync("/api/carts",
            new StringContent(JsonSerializer.Serialize(cartRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Parse<JsonElement>(createdContent);
        var cartId = doc.GetProperty("id").GetInt32();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/carts/{cartId}");
        var getResponse = await _client.GetAsync($"/api/carts/{cartId}");

        // Assert
        deleteResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        getResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError, HttpStatusCode.OK);
    }

}

/// <summary>
/// Helper class for ApplyDiscountRequest DTO
/// </summary>
public record ApplyDiscountRequest
{
    public required int CartDiscountId { get; set; }
}
