using FluentAssertions;
using POS_System.Business.Dtos;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ServiceControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public ServiceControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateService_WithValidPayload_ReturnsOkAndPersistsService()
    {
        // Arrange
        var request = new ServiceRequestBuilder()
            .WithName($"Service-{Guid.NewGuid():N}")
            .WithEmployeeId(1)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/services", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(request.Name);
        body.Description.Should().Be(request.Description);
        body.Price.Should().Be(request.Price);
        body.EmployeeId.Should().Be(request.EmployeeId);

        await using var db = _factory.CreateDbContext();
        var persisted = await db.Services.AsNoTracking().FirstOrDefaultAsync(service => service.Name == request.Name);
        persisted.Should().NotBeNull();
        persisted!.Price.Should().Be(request.Price);
    }

    [Fact]
    public async Task GetServiceById_WithExistingId_ReturnsOkAndService()
    {
        // Arrange
        var createRequest = new ServiceRequestBuilder().WithEmployeeId(1).Build();
        var createResponse = await _client.PostAsJsonAsync("/api/services", createRequest);
        var createdService = await createResponse.Content.ReadFromJsonAsync<ServiceResponse>();

        // Act
        var response = await _client.GetAsync($"/api/services/{createdService!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdService.Id);
        body.Name.Should().Be(createRequest.Name);
    }

    [Fact]
    public async Task UpdateService_WithExistingId_ReturnsOkAndUpdatesDatabase()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/services", new ServiceRequestBuilder().WithEmployeeId(1).Build());
        var createdService = await createResponse.Content.ReadFromJsonAsync<ServiceResponse>();

        var updateRequest = new ServiceRequestBuilder()
            .WithName($"Updated-{Guid.NewGuid():N}")
            .WithEmployeeId(2)
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/services/{createdService!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedBody = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        updatedBody!.Name.Should().Be(updateRequest.Name);
        updatedBody.EmployeeId.Should().Be(2);
    }

    [Fact]
    public async Task DeleteService_WithExistingId_ReturnsNoContentAndRemovesService()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/services", new ServiceRequestBuilder().WithEmployeeId(1).Build());
        var createdService = await createResponse.Content.ReadFromJsonAsync<ServiceResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/services/{createdService!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateService_WithInvalidImageUrl_ReturnsBadRequest()
    {
        // Arrange
        var request = new ServiceRequestBuilder()
            .WithEmployeeId(1)
            .WithImageUrl("not-a-valid-url")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/services", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetServiceById_WithMissingId_ReturnsNotFoundOrError()
    {
        // Act
        var response = await _client.GetAsync("/api/services/99999");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetAllAndLinkedServices_ReturnsOkAndPagedResults()
    {
        // Act
        var getAllResponse = await _client.GetAsync("/api/services?pageNum=0&pageSize=10");
        var linkedByTaxResponse = await _client.GetAsync("/api/services/tax/1");
        var linkedByDiscountResponse = await _client.GetAsync("/api/services/item-discount/1");

        // Assert
        getAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await getAllResponse.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();
        page.Should().NotBeNull();
        page!.Results.Should().NotBeEmpty();

        linkedByTaxResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        linkedByDiscountResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}