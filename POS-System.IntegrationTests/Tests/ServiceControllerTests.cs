using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class ServiceControllerTests(PosWebApplicationFactory factory)
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

    // GET /api/services
    [Fact]
    public async Task GetAllServices_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/services");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllServices_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/services");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // POST /api/services
    [Fact]
    public async Task CreateService_ValidRequest_ReturnsOk()
    {
        var request = new ServiceRequest
        {
            Name = "IntTest Service",
            Description = "Test desc",
            Duration = 30,
            Price = 1500,
            ImageURL = "https://example.com/image.jpg",
            EmployeeId = 1
        };
        var response = await _client.PostAsJsonAsync("/api/services", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateService_NoToken_ReturnsUnauthorized()
    {
        var request = new ServiceRequest { Name = "S", Description = "D", Duration = 30, Price = 100, ImageURL = "https://example.com/image.jpg", EmployeeId = 1 };
        var response = await _anonClient.PostAsJsonAsync("/api/services", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // PUT /api/services/{id}
    [Fact]
    public async Task UpdateService_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/services",
            new ServiceRequest { Name = "ToUpdate", Description = "D", Duration = 15, Price = 999, ImageURL = "https://example.com/image.jpg", EmployeeId = 1 });
        var svc = await created.Content.ReadFromJsonAsync<IdResponse>();

        var updateReq = new ServiceRequest { Name = "Updated Svc", Description = "Updated D", Duration = 20, Price = 1200, ImageURL = "https://example.com/image.jpg", EmployeeId = 1 };
        var response = await _client.PutAsJsonAsync($"/api/services/{svc!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateService_NonExistentId_ReturnsNotFound()
    {
        var updateReq = new ServiceRequest { Name = "X", Description = "D", Duration = 10, Price = 100, ImageURL = "https://example.com/image.jpg", EmployeeId = 1 };
        var response = await _client.PutAsJsonAsync("/api/services/99999", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/services/{id}
    [Fact]
    public async Task DeleteService_ExistingId_ReturnsNoContent()
    {
        var created = await _client.PostAsJsonAsync("/api/services",
            new ServiceRequest { Name = "ToDelete", Description = "D", Duration = 15, Price = 500, ImageURL = "https://example.com/image.jpg", EmployeeId = 1 });
        var svc = await created.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.DeleteAsync($"/api/services/{svc!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteService_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/services/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /api/services/tax/{id}
    [Fact]
    public async Task GetServicesLinkedToTaxId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/services/tax/2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetServicesLinkedToTaxId_NonExistent_ReturnsOkEmptyList()
    {
        var response = await _client.GetAsync("/api/services/tax/99999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // GET /api/services/item-discount/{id}
    [Fact]
    public async Task GetServicesLinkedToItemDiscountId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/services/item-discount/2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetServicesLinkedToItemDiscountId_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/services/item-discount/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record IdResponse(int Id);
}
