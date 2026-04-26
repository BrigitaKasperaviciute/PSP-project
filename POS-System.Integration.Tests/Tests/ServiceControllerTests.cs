using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class ServiceControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithAllClaims();
    private readonly HttpClient _anonClient = factory.CreateClient();

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/services");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingService_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/services/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistingService_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/services/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidService_ReturnsOk()
    {
        var request = new
        {
            Name = "Test Service",
            Description = "Test service description",
            Duration = 60,
            Price = 2000,
            ImageURL = "http://example.com/service.jpg",
            EmployeeId = 1
        };

        var response = await _client.PostAsJsonAsync("/api/services", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ExistingService_ReturnsOk()
    {
        var createRequest = new
        {
            Name = "Service To Update",
            Description = "Desc",
            Duration = 30,
            Price = 1500,
            ImageURL = "http://example.com/img.jpg",
            EmployeeId = 1
        };
        var createResponse = await _client.PostAsJsonAsync("/api/services", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceResponse>();

        var updateRequest = new
        {
            Name = "Updated Service",
            Description = "Updated Desc",
            Duration = 45,
            Price = 1800,
            ImageURL = "http://example.com/img2.jpg",
            EmployeeId = 1
        };
        var response = await _client.PutAsJsonAsync($"/api/services/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_ExistingService_ReturnsNoContent()
    {
        var createRequest = new
        {
            Name = "Service To Delete",
            Description = "Del Desc",
            Duration = 20,
            Price = 500,
            ImageURL = "http://example.com/img.jpg",
            EmployeeId = 1
        };
        var createResponse = await _client.PostAsJsonAsync("/api/services", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceResponse>();

        var response = await _client.DeleteAsync($"/api/services/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetServicesLinkedToTax_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/services/tax/2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetServicesLinkedToItemDiscount_ExistingDiscount_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/services/item-discount/2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetServicesLinkedToItemDiscount_NonExistingDiscount_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/services/item-discount/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
