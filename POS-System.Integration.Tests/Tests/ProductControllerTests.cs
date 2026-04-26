using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class ProductControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithAllClaims();
    private readonly HttpClient _anonClient = factory.CreateClient();

   [Fact]
    public async Task GetById_ExistingProduct_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product/4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistingProduct_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/product/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetVersionsByProductId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product/1/versions/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_ValidProduct_ReturnsOk()
    {
        var request = new
        {
            Name = "Test Product",
            Description = "Test Description",
            Price = 999,
            ImageURL = "http://example.com/img.jpg",
            Stock = 10
        };

        var response = await _client.PostAsJsonAsync("/api/product", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_NullBody_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync<object?>("/api/product", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ExistingProduct_ReturnsOk()
    {
        var createRequest = new
        {
            Name = "Product To Update",
            Description = "Desc",
            Price = 500,
            ImageURL = "http://example.com/img.jpg",
            Stock = 5
        };
        var createResponse = await _client.PostAsJsonAsync("/api/product", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();

        var updateRequest = new
        {
            Name = "Updated Product",
            Description = "Updated Desc",
            Price = 600,
            ImageURL = "http://example.com/img2.jpg",
            Stock = 8
        };
        var response = await _client.PutAsJsonAsync($"/api/product/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_NonExistingProduct_ReturnsNotFound()
    {
        var request = new
        {
            Name = "Ghost",
            Description = "Ghost desc",
            Price = 100,
            ImageURL = "http://example.com/img.jpg",
            Stock = 1
        };

        var response = await _client.PutAsJsonAsync("/api/product/99999", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingProduct_ReturnsOk()
    {
        var createRequest = new
        {
            Name = "Product To Delete",
            Description = "Del Desc",
            Price = 100,
            ImageURL = "http://example.com/img.jpg",
            Stock = 1
        };
        var createResponse = await _client.PostAsJsonAsync("/api/product", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();

        var response = await _client.DeleteAsync($"/api/product/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductsLinkedToTax_ExistingTax_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product/tax/2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductsLinkedToItemDiscount_ExistingDiscount_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product/item-discount/2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
