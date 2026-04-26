using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class CartControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/carts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_ExistingCart_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/carts/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistingCart_ReturnsError()
    {
        var response = await _client.GetAsync("/api/carts/99999");

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOk()
    {
        var request = new { EmployeeVersionId = 1 };

        var response = await _client.PostAsJsonAsync("/api/carts", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_InProgressCart_ReturnsOk()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/carts", new { EmployeeVersionId = 1 });
        var cart = await createResponse.Content.ReadFromJsonAsync<CartResponse>();

        var deleteResponse = await _client.DeleteAsync($"/api/carts/{cart!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_PendingCart_ReturnsError()
    {
        var response = await _client.DeleteAsync("/api/carts/1");

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCartDiscount_CartWithoutDiscount_ReturnsNoContent()
    {
        var response = await _client.GetAsync("/api/carts/1/discount");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
