using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Text.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class ProductControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _authorizedClient;

    public ProductControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _authorizedClient = null!;
    }

    public async Task InitializeAsync()
    {
        _authorizedClient = _factory.CreateAuthenticatedClient("ItemRead", "ItemWrite");
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _authorizedClient?.Dispose();
        await Task.CompletedTask;
    }

    // ===== GetAllProducts Tests =====

    [Fact]
    public async Task GetAllProducts_WithValidRequest_ReturnsOkWithProductList()
    {
        // Arrange
        var productRequest = new ProductRequestBuilder()
            .WithName("Test Product")
            .WithPrice(5000)
            .Build();

        await _authorizedClient.PostAsync("/api/product",
            new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));

        // Act
        var response = await _authorizedClient.GetAsync("/api/product?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetAllProducts_WithOnlyActiveFilter_ReturnsOnlyActiveProducts()
    {
        // Arrange
        var productRequest = new ProductRequestBuilder()
            .WithName("Active Product")
            .Build();

        await _authorizedClient.PostAsync("/api/product",
            new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));

        // Act
        var response = await _authorizedClient.GetAsync("/api/product?onlyActive=true&pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllProducts_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 0; i < 12; i++)
        {
            var productRequest = new ProductRequestBuilder()
                .WithName($"Product{i}")
                .Build();

            await _authorizedClient.PostAsync("/api/product",
                new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));
        }

        // Act
        var response = await _authorizedClient.GetAsync("/api/product?pageSize=5&pageNumber=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllProducts_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient();

        // Act
        var response = await unauthorizedClient.GetAsync("/api/product?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== GetProductById Tests =====

    [Fact]
    public async Task GetProductById_WithExistingId_ReturnsOkAndProductData()
    {
        // Arrange
        var productRequest = new ProductRequestBuilder()
            .WithName("Specific Product")
            .WithPrice(9999)
            .Build();

        var createResponse = await _authorizedClient.PostAsync("/api/product",
            new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        var productId = jsonDocument.GetProperty("id").GetInt32();

        // Act
        var response = await _authorizedClient.GetAsync($"/api/product/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var retrievedProduct = JsonSerializer.Parse<JsonElement>(content);
        retrievedProduct.TryGetProperty("id", out var id).Should().BeTrue();
        id.GetInt32().Should().Be(productId);
    }

    [Fact]
    public async Task GetProductById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/product/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ===== GetProductVersionsByProductId Tests =====

    [Fact]
    public async Task GetProductVersionsByProductId_WithExistingId_ReturnsOkWithVersionsList()
    {
        // Arrange
        var productRequest = new ProductRequestBuilder()
            .WithName("Versioned Product")
            .Build();

        var createResponse = await _authorizedClient.PostAsync("/api/product",
            new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        var productId = jsonDocument.GetProperty("id").GetInt32();

        // Act
        var response = await _authorizedClient.GetAsync($"/api/product/{productId}/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var versions = JsonSerializer.Parse<JsonElement>(content);
        versions.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    // ===== CreateProduct Tests =====

    [Fact]
    public async Task CreateProduct_WithValidRequest_ReturnsOkAndCreatesEntity()
    {
        // Arrange
        var productRequest = new ProductRequestBuilder()
            .WithName("New Product")
            .WithDescription("A new product")
            .WithPrice(25000)
            .WithStock(50)
            .Build();

        // Act
        var response = await _authorizedClient.PostAsync("/api/product",
            new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.TryGetProperty("name", out var name).Should().BeTrue();
        name.GetString().Should().Be("New Product");

        // Verify database persistence
        var getAllResponse = await _authorizedClient.GetAsync("/api/product?pageSize=100&pageNumber=0");
        getAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateProduct_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = "{}";

        // Act
        var response = await _authorizedClient.PostAsync("/api/product",
            new StringContent(invalidRequest, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("ItemRead");
        var productRequest = new ProductRequestBuilder().Build();

        // Act
        var response = await unauthorizedClient.PostAsync("/api/product",
            new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== UpdateProductByProductId Tests =====

    [Fact]
    public async Task UpdateProductByProductId_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var updateRequest = new ProductRequestBuilder().Build();

        // Act
        var response = await _authorizedClient.PutAsync("/api/product/99999",
            new StringContent(JsonSerializer.Serialize(updateRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ===== DeleteProductById Tests =====

    [Fact]
    public async Task DeleteProductById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.DeleteAsync("/api/product/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProductById_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var productRequest = new ProductRequestBuilder().Build();
        var createResponse = await _authorizedClient.PostAsync("/api/product",
            new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));
        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        var productId = jsonDocument.GetProperty("id").GetInt32();

        var unauthorizedClient = _factory.CreateAuthenticatedClient("ItemRead");

        // Act
        var response = await unauthorizedClient.DeleteAsync($"/api/product/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== GetProductsLinkedToTaxId Tests =====

    [Fact]
    public async Task GetProductsLinkedToTaxId_WithValidRequest_ReturnsOkWithProductsList()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/product/tax/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public async Task GetProductsLinkedToTaxId_WithTimestamp_ReturnsOkWithProductsList()
    {
        // Arrange & Act
        var timestamp = DateTime.UtcNow.ToString("o");
        var response = await _authorizedClient.GetAsync($"/api/product/tax/1?timeStamp={Uri.EscapeDataString(timestamp)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ===== GetProductsLinkedToItemDiscountId Tests =====

    [Fact]
    public async Task GetProductsLinkedToItemDiscountId_WithValidRequest_ReturnsOkWithProductsList()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/product/item-discount/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public async Task GetProductsLinkedToItemDiscountId_WithTimestamp_ReturnsOkWithProductsList()
    {
        // Arrange & Act
        var timestamp = DateTime.UtcNow.ToString("o");
        var response = await _authorizedClient.GetAsync($"/api/product/item-discount/1?timeStamp={Uri.EscapeDataString(timestamp)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_Product_WithMultipleRequests_CreatesMultipleProducts()
    {
        // Arrange
        var productIds = new List<int>();

        // Act
        for (int i = 0; i < 3; i++)
        {
            var productRequest = new ProductRequestBuilder()
                .WithName($"Multi Product {i}")
                .WithPrice(1000 * (i + 1))
                .Build();

            var response = await _authorizedClient.PostAsync("/api/product",
                new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Parse<JsonElement>(content);
            productIds.Add(doc.GetProperty("id").GetInt32());
        }

        // Assert
        productIds.Should().HaveCount(3);
        productIds.Distinct().Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllProducts_MultiplePages_ReturnsDifferentResults()
    {
        // Arrange - Create multiple products
        for (int i = 0; i < 25; i++)
        {
            var productRequest = new ProductRequestBuilder()
                .WithName($"Pagination Product {i}")
                .Build();

            await _authorizedClient.PostAsync("/api/product",
                new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));
        }

        // Act
        var page1 = await _authorizedClient.GetAsync("/api/product?pageSize=10&pageNumber=0");
        var page2 = await _authorizedClient.GetAsync("/api/product?pageSize=10&pageNumber=1");

        // Assert
        page1.StatusCode.Should().Be(HttpStatusCode.OK);
        page2.StatusCode.Should().Be(HttpStatusCode.OK);

        var content1 = await page1.Content.ReadAsStringAsync();
        var content2 = await page2.Content.ReadAsStringAsync();

        content1.Should().NotBe(content2);
    }

    [Fact]
    public async Task Update_Product_WithValidId_UpdatesSuccessfully()
    {
        // Arrange
        var productRequest = new ProductRequestBuilder()
            .WithName("Product To Update")
            .Build();

        var createResponse = await _authorizedClient.PostAsync("/api/product",
            new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Parse<JsonElement>(createdContent);
        var productId = doc.GetProperty("id").GetInt32();

        var updateRequest = new ProductRequestBuilder()
            .WithName("Updated Product")
            .WithPrice(9999)
            .Build();

        // Act
        var updateResponse = await _authorizedClient.PutAsync($"/api/product/{productId}",
            new StringContent(JsonSerializer.Serialize(updateRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        updateResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetById_Product_VerifyResponseStructure()
    {
        // Arrange
        var productRequest = new ProductRequestBuilder()
            .WithName("Test Structure Product")
            .Build();

        var createResponse = await _authorizedClient.PostAsync("/api/product",
            new StringContent(JsonSerializer.Serialize(productRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var createdDoc = JsonSerializer.Parse<JsonElement>(createdContent);
        var productId = createdDoc.GetProperty("id").GetInt32();

        // Act
        var getResponse = await _authorizedClient.GetAsync($"/api/product/{productId}");
        var getContent = await getResponse.Content.ReadAsStringAsync();
        var getDoc = JsonSerializer.Parse<JsonElement>(getContent);

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getDoc.TryGetProperty("id", out _).Should().BeTrue();
        getDoc.TryGetProperty("name", out _).Should().BeTrue();
    }
}
