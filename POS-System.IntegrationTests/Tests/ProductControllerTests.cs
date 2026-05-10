using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class ProductControllerTests(PosWebApplicationFactory factory)
{
    private readonly HttpClient _client = CreateAuthClient(factory);
    private readonly HttpClient _anonClient = factory.CreateClient();

    private static HttpClient CreateAuthClient(PosWebApplicationFactory f)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.FullAccessToken);
        return c;
    }

    // GET /api/product
    [Fact]
    public async Task GetAllProducts_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllProducts_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/product");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // GET /api/product/{id}
    [Fact]
    public async Task GetProductById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product/4");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProductById_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/product/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /api/product/{productId}/versions/
    [Fact]
    public async Task GetProductVersions_ExistingProductId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product/1/versions/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProductVersions_NonExistentProductId_ReturnsOkEmptyList()
    {
        var response = await _client.GetAsync("/api/product/99999/versions/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // POST /api/product
    [Fact]
    public async Task CreateProduct_ValidRequest_ReturnsOk()
    {
        var request = new ProductRequest
        {
            Name = "IntTest Product",
            Description = "Test desc",
            Price = 999,
            ImageURL = "https://example.com/image.jpg",
            Stock = 10
        };
        var response = await _client.PostAsJsonAsync("/api/product", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_NoToken_ReturnsUnauthorized()
    {
        var request = new ProductRequest { Name = "P", Description = "D", Price = 1, ImageURL = "https://example.com/image.jpg", Stock = 1 };
        var response = await _anonClient.PostAsJsonAsync("/api/product", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // PUT /api/product/{id}
    [Fact]
    public async Task UpdateProduct_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/product",
            new ProductRequest { Name = "ToUpdate", Description = "D", Price = 500, ImageURL = "https://example.com/image.jpg", Stock = 5 });
        var product = await created.Content.ReadFromJsonAsync<IdResponse>();

        var updateReq = new ProductRequest { Name = "Updated", Description = "Updated D", Price = 600, ImageURL = "https://example.com/image.jpg", Stock = 6 };
        var response = await _client.PutAsJsonAsync($"/api/product/{product!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_NonExistentId_ReturnsNotFound()
    {
        var updateReq = new ProductRequest { Name = "X", Description = "D", Price = 1, ImageURL = "https://example.com/image.jpg", Stock = 1 };
        var response = await _client.PutAsJsonAsync("/api/product/99999", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/product/{id}
    [Fact]
    public async Task DeleteProduct_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/product",
            new ProductRequest { Name = "ToDelete", Description = "D", Price = 100, ImageURL = "https://example.com/image.jpg", Stock = 1 });
        var product = await created.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.DeleteAsync($"/api/product/{product!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/product/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /api/product/tax/{id}
    [Fact]
    public async Task GetProductsLinkedToTaxId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product/tax/2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProductsLinkedToTaxId_NonExistentTax_ReturnsOkEmptyList()
    {
        var response = await _client.GetAsync("/api/product/tax/99999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // GET /api/product/item-discount/{id}
    [Fact]
    public async Task GetProductsLinkedToItemDiscountId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product/item-discount/2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProductsLinkedToItemDiscountId_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/product/item-discount/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record IdResponse(int Id);
}
