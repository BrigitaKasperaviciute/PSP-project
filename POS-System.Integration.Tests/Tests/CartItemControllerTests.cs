using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class CartItemControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithAllClaims();
    private readonly HttpClient _anonClient = factory.CreateClient();

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/carts/1/items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/carts/1/items");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingItem_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/carts/1/items/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistingItem_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/carts/1/items/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidCartItem_ReturnsOk()
    {
        var request = new
        {
            CartId = 3,
            Quantity = 2,
            IsProduct = true,
            ProductVersionId = 4
        };

        var response = await _client.PostAsJsonAsync("/api/carts/3/items", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ExistingCartItem_ReturnsOk()
    {
        var createRequest = new
        {
            CartId = 3,
            Quantity = 1,
            IsProduct = true,
            ProductVersionId = 4
        };
        var createResponse = await _client.PostAsJsonAsync("/api/carts/3/items", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        var updateRequest = new
        {
            CartId = 3,
            Quantity = 3,
            IsProduct = true,
            ProductVersionId = 4
        };
        var response = await _client.PutAsJsonAsync($"/api/carts/3/items/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_ExistingCartItem_ReturnsNoContent()
    {
        var createRequest = new
        {
            CartId = 3,
            Quantity = 1,
            IsProduct = true,
            ProductVersionId = 4
        };
        var createResponse = await _client.PostAsJsonAsync("/api/carts/3/items", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        var response = await _client.DeleteAsync($"/api/carts/3/items/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task LinkCartItemToProductModifications_ReturnsOk()
    {
        var createRequest = new
        {
            CartId = 3,
            Quantity = 1,
            IsProduct = true,
            ProductVersionId = 4
        };
        var createResponse = await _client.PostAsJsonAsync("/api/carts/3/items", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        var response = await _client.PutAsJsonAsync($"/api/carts/3/items/{created!.Id}/link", new[] { 2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlinkCartItemFromProductModifications_ReturnsOk()
    {
        var createRequest = new
        {
            CartId = 3,
            Quantity = 1,
            IsProduct = true,
            ProductVersionId = 4
        };
        var createResponse = await _client.PostAsJsonAsync("/api/carts/3/items", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CartItemResponse>();

        await _client.PutAsJsonAsync($"/api/carts/3/items/{created!.Id}/link", new[] { 2 });

        var response = await _client.PutAsJsonAsync($"/api/carts/3/items/{created.Id}/unlink", new[] { 2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
