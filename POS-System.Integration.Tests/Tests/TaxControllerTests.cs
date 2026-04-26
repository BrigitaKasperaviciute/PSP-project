using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class TaxControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithAllClaims();
    private readonly HttpClient _anonClient = factory.CreateClient();

    [Fact]
    public async Task GetAll_WithTaxReadToken_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/tax");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/tax");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingTax_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/tax/2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistingTax_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/tax/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidTax_ReturnsOk()
    {
        var request = new { Name = "Test Tax", Rate = 15, IsPercentage = true };

        var response = await _client.PostAsJsonAsync("/api/tax", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ExistingTax_ReturnsOk()
    {
        var createRequest = new { Name = "Tax To Update", Rate = 10, IsPercentage = true };
        var createResponse = await _client.PostAsJsonAsync("/api/tax", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();

        var updateRequest = new { Name = "Updated Tax", Rate = 20, IsPercentage = false };
        var response = await _client.PutAsJsonAsync($"/api/tax/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_ExistingTax_ReturnsOk()
    {
        var createRequest = new { Name = "Tax To Delete", Rate = 5, IsPercentage = true };
        var createResponse = await _client.PostAsJsonAsync("/api/tax", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();

        var response = await _client.DeleteAsync($"/api/tax/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkAndUnlink_ProductsToTax_ReturnsOk()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/tax", new { Name = "Link Tax", Rate = 3, IsPercentage = true });
        var tax = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();

        var linkResponse = await _client.PutAsJsonAsync($"/api/tax/{tax!.Id}/link?itemsAreProducts=true", new[] { 4 });
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var unlinkResponse = await _client.PutAsJsonAsync($"/api/tax/{tax.Id}/unlink?itemsAreProducts=true", new[] { 4 });
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTaxesLinkedToItem_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/tax/item/4?isProduct=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
