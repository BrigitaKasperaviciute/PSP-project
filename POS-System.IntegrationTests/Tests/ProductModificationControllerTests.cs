using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class ProductModificationControllerTests(PosWebApplicationFactory factory)
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

    // GET /api/product-modification
    [Fact]
    public async Task GetAllProductModifications_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product-modification");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllProductModifications_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/product-modification");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // GET /api/product-modification/{id}
    [Fact]
    public async Task GetProductModificationById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product-modification/2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProductModificationById_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/product-modification/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /api/product-modification/{id}/versions/
    [Fact]
    public async Task GetProductModificationVersions_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product-modification/1/versions/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProductModificationVersions_NonExistentId_ReturnsOkEmptyList()
    {
        var response = await _client.GetAsync("/api/product-modification/99999/versions/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // POST /api/product-modification
    [Fact]
    public async Task CreateProductModification_ValidRequest_ReturnsOk()
    {
        var request = new ProductModificationRequest
        {
            ProductVersionId = 4,
            Name = "IntTest Mod",
            Description = "Test desc",
            Price = 150
        };
        var response = await _client.PostAsJsonAsync("/api/product-modification", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateProductModification_NoToken_ReturnsUnauthorized()
    {
        var request = new ProductModificationRequest { ProductVersionId = 1, Name = "M", Description = "D", Price = 10 };
        var response = await _anonClient.PostAsJsonAsync("/api/product-modification", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // PUT /api/product-modification/{id}
    [Fact]
    public async Task UpdateProductModification_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/product-modification",
            new ProductModificationRequest { ProductVersionId = 4, Name = "ToUpdate", Description = "D", Price = 100 });
        var mod = await created.Content.ReadFromJsonAsync<IdResponse>();

        var updateReq = new ProductModificationRequest { ProductVersionId = 4, Name = "Updated Mod", Description = "Updated D", Price = 200 };
        var response = await _client.PutAsJsonAsync($"/api/product-modification/{mod!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProductModification_NonExistentId_ReturnsNotFound()
    {
        var updateReq = new ProductModificationRequest { ProductVersionId = 1, Name = "X", Description = "D", Price = 10 };
        var response = await _client.PutAsJsonAsync("/api/product-modification/99999", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/product-modification/{id}
    [Fact]
    public async Task DeleteProductModification_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/product-modification",
            new ProductModificationRequest { ProductVersionId = 4, Name = "ToDelete", Description = "D", Price = 50 });
        var mod = await created.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.DeleteAsync($"/api/product-modification/{mod!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProductModification_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/product-modification/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /api/product-modification/cart-item/{id}
    [Fact]
    public async Task GetProductModificationsLinkedToCartItemId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product-modification/cart-item/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProductModificationsLinkedToCartItemId_NonExistent_ReturnsOkEmptyList()
    {
        var response = await _client.GetAsync("/api/product-modification/cart-item/99999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // GET /api/product-modification/product/{id}
    [Fact]
    public async Task GetProductModificationsLinkedToProductId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product-modification/product/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProductModificationsLinkedToProductId_NonExistent_ReturnsOkEmptyList()
    {
        var response = await _client.GetAsync("/api/product-modification/product/99999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private record IdResponse(int Id);
}
