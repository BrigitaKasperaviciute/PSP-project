using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class ProductModificationControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithAllClaims();
    private readonly HttpClient _anonClient = factory.CreateClient();

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product-modification");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/product-modification");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingModification_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product-modification/2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistingModification_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/product-modification/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetVersionsByProductModificationId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product-modification/1/versions/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_ValidModification_ReturnsOk()
    {
        var request = new
        {
            Name = "Extra Sauce",
            Description = "Add extra sauce",
            Price = 50,
            ProductVersionId = 4
        };

        var response = await _client.PostAsJsonAsync("/api/product-modification", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ExistingModification_ReturnsOk()
    {
        var createRequest = new
        {
            Name = "Mod To Update",
            Description = "Mod desc",
            Price = 100,
            ProductVersionId = 4
        };
        var createResponse = await _client.PostAsJsonAsync("/api/product-modification", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductModificationResponse>();

        var updateRequest = new
        {
            Name = "Updated Mod",
            Description = "Updated desc",
            Price = 150,
            ProductVersionId = 4
        };
        var response = await _client.PutAsJsonAsync($"/api/product-modification/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_ExistingModification_ReturnsOk()
    {
        var createRequest = new
        {
            Name = "Mod To Delete",
            Description = "Del mod desc",
            Price = 75,
            ProductVersionId = 4
        };
        var createResponse = await _client.PostAsJsonAsync("/api/product-modification", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductModificationResponse>();

        var response = await _client.DeleteAsync($"/api/product-modification/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetModificationsLinkedToProduct_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product-modification/product/4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetModificationsLinkedToCartItem_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/product-modification/cart-item/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
