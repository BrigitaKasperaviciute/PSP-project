using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ServiceControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public ServiceControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        await _db.Services.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateService_WithValidPayload_ReturnsOkAndPersistsService()
    {
        // Arrange
        var request = new ServiceRequestBuilder()
            .WithName("Haircut")
            .WithDuration(30)
            .WithPrice(25.00m)
            .WithEmployeeId(1)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/services", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.Name.Should().Be(request.Name);

        // Assert - database state
        var persisted = await _db.Services.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task GetAllServices_WithValidPageNumbers_ReturnsOkWithServices()
    {
        // Act
        var response = await _client.GetAsync("/api/services?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<ServiceResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetServiceById_WithExistingId_ReturnsOkWithService()
    {
        // Arrange
        var request = new ServiceRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/services", request);
        var createdService = await createResponse.Content.ReadFromJsonAsync<ServiceResponse>();

        // Act
        var response = await _client.GetAsync($"/api/services/{createdService!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body!.Id.Should().Be(createdService.Id);
    }

    [Fact]
    public async Task UpdateService_WithValidPayload_ReturnsOkAndUpdatesService()
    {
        // Arrange
        var createRequest = new ServiceRequestBuilder().WithName("OldName").Build();
        var createResponse = await _client.PostAsJsonAsync("/api/services", createRequest);
        var createdService = await createResponse.Content.ReadFromJsonAsync<ServiceResponse>();

        // Act
        var updateRequest = new ServiceRequestBuilder().WithName("UpdatedName").Build();
        var response = await _client.PutAsJsonAsync($"/api/services/{createdService!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body!.Name.Should().Be(updateRequest.Name);
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetServiceById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/services/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateService_WithNullName_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = """{"name": null, "description": "Test", "duration": 30, "price": 25, "imageURL": "", "employeeId": 1}""";

        // Act
        var response = await _client.PostAsJsonAsync("/api/services", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateService_WithNegativePrice_ReturnsBadRequest()
    {
        // Arrange
        var request = new ServiceRequestBuilder().WithPrice(-5.00m).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/services", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateService_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ServiceRequestBuilder().Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/services/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

public class ServiceResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Duration { get; set; }
    public decimal Price { get; set; }
    public string ImageURL { get; set; } = "";
    public int EmployeeId { get; set; }
    public bool IsActive { get; set; }
}
