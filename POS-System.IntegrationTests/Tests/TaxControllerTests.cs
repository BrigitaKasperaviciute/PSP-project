using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class TaxControllerTests(PosWebApplicationFactory factory)
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

    // GET /api/tax
    [Fact]
    public async Task GetAllTaxes_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/tax");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllTaxes_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/tax");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // POST /api/tax
    [Fact]
    public async Task CreateTax_ValidRequest_ReturnsOk()
    {
        var request = new TaxRequest { Name = "IntTest Tax", Rate = 10, IsPercentage = true };
        var response = await _client.PostAsJsonAsync("/api/tax", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateTax_NoToken_ReturnsUnauthorized()
    {
        var request = new TaxRequest { Name = "Tax", Rate = 10, IsPercentage = true };
        var response = await _anonClient.PostAsJsonAsync("/api/tax", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // GET /api/tax/{id}
    [Fact]
    public async Task GetTaxById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/tax/2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTaxById_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/tax/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/tax/{id}
    [Fact]
    public async Task DeleteTax_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/tax",
            new TaxRequest { Name = "ToDelete", Rate = 5, IsPercentage = true });
        var tax = await created.Content.ReadFromJsonAsync<TaxIdResponse>();

        var response = await _client.DeleteAsync($"/api/tax/{tax!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTax_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/tax/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PUT /api/tax/{id}
    [Fact]
    public async Task UpdateTax_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/tax",
            new TaxRequest { Name = "ToUpdate", Rate = 5, IsPercentage = true });
        var tax = await created.Content.ReadFromJsonAsync<TaxIdResponse>();

        var updateReq = new TaxRequest { Name = "Updated Tax", Rate = 8, IsPercentage = true };
        var response = await _client.PutAsJsonAsync($"/api/tax/{tax!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTax_NonExistentId_ReturnsNotFound()
    {
        var updateReq = new TaxRequest { Name = "X", Rate = 5, IsPercentage = true };
        var response = await _client.PutAsJsonAsync("/api/tax/99999", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PUT /api/tax/{id}/link
    [Fact]
    public async Task LinkTaxToItems_ValidIds_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/tax/4/link?itemsAreProducts=true", new[] { 4 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LinkTaxToItems_NonExistentTax_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/tax/99999/link?itemsAreProducts=true", new[] { 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // PUT /api/tax/{id}/unlink
    [Fact]
    public async Task UnlinkTaxFromItems_ValidIds_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/tax/4/unlink?itemsAreProducts=true", new[] { 4 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkTaxFromItems_NonExistentTax_ReturnsOk()
    {
        var response = await _client.PutAsJsonAsync("/api/tax/99999/unlink?itemsAreProducts=true", new[] { 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // GET /api/tax/item/{id}
    [Fact]
    public async Task GetTaxesLinkedToItemId_ExistingItem_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/tax/item/4?isProduct=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTaxesLinkedToItemId_NonExistentItem_ReturnsOkEmptyList()
    {
        var response = await _client.GetAsync("/api/tax/item/99999?isProduct=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private record TaxIdResponse(int Id);
}
