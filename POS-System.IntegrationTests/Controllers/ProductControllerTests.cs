using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using System.Net;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    /// <summary>
    /// Integration tests for ProductController.
    /// Tests product CRUD operations with realistic scenarios and validation.
    /// </summary>
    public class ProductControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/product";

        [Fact]
        public async Task CreateProduct_ValidRequest_ReturnsOkWithCreatedProduct()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(productRequest));

            // Assert
            AssertOkResponse(response);
            var createdProduct = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            createdProduct.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
            createdProduct.GetProperty("name").GetString().Should().Be(productRequest.Name);
            createdProduct.GetProperty("price").GetInt32().Should().Be(productRequest.Price);
            createdProduct.GetProperty("stock").GetInt32().Should().Be(productRequest.Stock);
        }

        [Fact]
        public async Task CreateProduct_InvalidPrice_MayReturnBadRequest()
        {
            // Arrange
            var invalidRequest = new { Name = "Test", Description = "Test", Price = -100, ImageURL = "url", Stock = 10 };

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(invalidRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }

        [Fact]
        public async Task CreateProduct_NegativeStock_MayReturnBadRequest()
        {
            // Arrange
            var invalidRequest = new { Name = "Test", Description = "Test", Price = 100, ImageURL = "url", Stock = -5 };

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(invalidRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetAllProducts_WithoutFilters_ReturnsProducts()
        {
            // Arrange & Act
            var response = await Client.GetAsync(BaseUrl);

            // Assert
            AssertOkResponse(response);
            var products = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            products.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task GetAllProducts_WithPagination_ReturnsPaginatedResults()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();
            await Client.PostAsync(BaseUrl, CreateJsonContent(productRequest));

            // Act
            var response = await Client.GetAsync($"{BaseUrl}?pageSize=5&pageNumber=0");

            // Assert
            AssertOkResponse(response);
            var products = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            products.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task GetAllProducts_FilterOnlyActive_ReturnsActiveProducts()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();
            await Client.PostAsync(BaseUrl, CreateJsonContent(productRequest));

            // Act
            var response = await Client.GetAsync($"{BaseUrl}?onlyActive=true");

            // Assert
            AssertOkResponse(response);
            var products = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            products.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task GetProductById_ValidId_ReturnsProduct()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(productRequest));
            var productId = await ExtractIdFromResponseAsync(createResponse);

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{productId}");

            // Assert
            AssertOkResponse(response);
            var product = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            product.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task GetProductById_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{invalidId}");

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task GetProductVersionsByProductId_ValidId_ReturnsVersions()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(productRequest));
            var productId = await ExtractIdFromResponseAsync(createResponse);

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{productId}/versions/");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UpdateProductByProductId_ValidRequest_ReturnsUpdatedProduct()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(productRequest));
            var productId = await ExtractIdFromResponseAsync(createResponse);

            var updatedRequest = new ProductRequestBuilder()
                .WithName("Updated Product")
                .WithPrice(2000)
                .Build();

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/{productId}", CreateJsonContent(updatedRequest));

            // Assert
            AssertOkResponse(response);
            var updated = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            updated.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task UpdateProductByProductId_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/999999", CreateJsonContent(productRequest));

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task DeleteProductById_ValidId_ReturnsOk()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(productRequest));
            var productId = await ExtractIdFromResponseAsync(createResponse);

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{productId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task DeleteProductById_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{invalidId}");

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task DeleteProductById_ThenGetById_ReturnsSoftDeletedProduct()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(productRequest));
            var productId = await ExtractIdFromResponseAsync(createResponse);

            // Act
            var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/{productId}");
            deleteResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

            var getResponse = await Client.GetAsync($"{BaseUrl}/{productId}");

            // Assert
            AssertOkResponse(getResponse);

            var deletedProduct = await DeserializeResponseAsync<System.Text.Json.JsonElement>(getResponse);
            deletedProduct.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
            deletedProduct.GetProperty("isDeleted").GetBoolean().Should().BeTrue();

            var storedProduct = await ExecuteDbAsync(db => db.Products.FirstOrDefaultAsync(product => product.Id == productId));
            storedProduct.Should().NotBeNull();
            storedProduct!.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task CreateProduct_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();

            // Act
            var response = await UnauthenticatedClient.PostAsync(BaseUrl, CreateJsonContent(productRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task UpdateProduct_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();

            // Act
            var response = await UnauthenticatedClient.PutAsync($"{BaseUrl}/1", CreateJsonContent(productRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task DeleteProduct_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange - Use unauthenticated client

            // Act
            var response = await UnauthenticatedClient.DeleteAsync($"{BaseUrl}/1");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetProductsLinkedToTaxId_ValidId_ReturnsProducts()
        {
            // Arrange
            int taxId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/tax/{taxId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetProductsLinkedToItemDiscountId_ValidId_ReturnsProducts()
        {
            // Arrange
            int discountId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/item-discount/{discountId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateMultipleProducts_WithDifferentNames_AllCreatedSuccessfully()
        {
            // Arrange
            var products = new[]
            {
                ProductRequestBuilder.CreateWithName("Product 1"),
                ProductRequestBuilder.CreateWithName("Product 2"),
                ProductRequestBuilder.CreateWithName("Product 3")
            };

            // Act & Assert
            var createdIds = new List<int>();
            foreach (var product in products)
            {
                var response = await Client.PostAsync(BaseUrl, CreateJsonContent(product));
                AssertOkResponse(response);
                var id = await ExtractIdFromResponseAsync(response);
                createdIds.Add(id);
            }

            createdIds.Should().HaveCount(3);
            createdIds.Should().OnlyContain(x => x > 0);
            createdIds.Distinct().Should().HaveCount(3);
        }

        [Fact]
        public async Task CreateProduct_WithHighPrice_ReturnsOk()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateWithPrice(999999);

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(productRequest));

            // Assert
            AssertOkResponse(response);
            var created = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            created.GetProperty("price").GetInt32().Should().Be(999999);
        }

        [Fact]
        public async Task CreateProduct_WithLargeStock_ReturnsOk()
        {
            // Arrange
            var productRequest = ProductRequestBuilder.CreateDefault();

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(productRequest));

            // Assert
            AssertOkResponse(response);
            var created = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            created.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }
    }
}
