using System.Net;
using Microsoft.AspNetCore.Http;
using Xunit;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Infrastructure;
using System.Collections.Generic;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ServiceControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ServiceControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    // Seeded services have Ids 1–4
    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.Services.Where(s => s.Id > 4).ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetAllServices ---------------

    [Fact]
    public async Task GetAllServices_WhenServicesExist_ReturnsOkWithPagedResults()
    {
        // Arrange – seeded services are present

        // Act
        var response = await _client.GetAsync("/api/services?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllServices_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/services?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- GetServiceById ---------------

    [Fact]
    public async Task GetServiceById_WhenServiceExists_ReturnsOkWithService()
    {
        // Arrange – seeded Service Id=1 (ServiceId=1, Name="Service1", IsDeleted=false)
        const int existingId = 1;

        // Act
        var response = await _client.GetAsync($"/api/services/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.Name.Should().Be("Service1");
    }

    [Fact]
    public async Task GetServiceById_WhenServiceDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/services/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- CreateService ---------------

    [Fact]
    public async Task CreateService_WithValidRequest_ReturnsOkAndPersistsService()
    {
        // Arrange – seeded employee Id=1
        var request = new ServiceRequest
        {
            Name = "Haircut",
            Description = "Standard haircut",
            Duration = 30,
            Price = 1500,
            ImageURL = "https://example.com/haircut.jpg",
            EmployeeId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/services", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(request.Name);
        body.Duration.Should().Be(request.Duration);
        body.Price.Should().Be(request.Price);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Services.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateService_WithMissingRequiredField_ReturnsBadRequest()
    {
        // Arrange – omit Name
        var payload = new
        {
            Description = "desc",
            Duration = 30,
            Price = 1000,
            ImageURL = "",
            EmployeeId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/services", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --------------- UpdateService ---------------

    [Fact]
    public async Task UpdateService_WithValidRequest_ReturnsOkWithNewVersion()
    {
        // Arrange
        var created = await CreateServiceAsync("Massage", "Deep tissue massage", 60, 3000, "https://example.com/massage.jpg", 1);
        var updateRequest = new ServiceRequest
        {
            Name = "Massage Premium",
            Description = "Hot stone massage",
            Duration = 90,
            Price = 5000,
            ImageURL = "https://example.com/massage-premium.jpg",
            EmployeeId = 1
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/services/{created.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(updateRequest.Name);
        body.Duration.Should().Be(updateRequest.Duration);

        // Assert – old version is soft-deleted, new version is active
        await using var db = _factory.CreateDbContext();
        var oldVersion = await db.Services.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == created.Id);
        oldVersion.Should().NotBeNull();
        oldVersion!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateService_WhenServiceDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var request = new ServiceRequest
        {
            Name = "X",
            Description = "x",
            Duration = 10,
            Price = 100,
            ImageURL = "https://example.com/x.jpg",
            EmployeeId = 1
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/services/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- DeleteService ---------------

    [Fact]
    public async Task DeleteService_WhenServiceExists_ReturnsNoContentAndSoftDeletes()
    {
        // Arrange
        var created = await CreateServiceAsync("DeleteMe", "desc", 15, 500, "https://example.com/delete.jpg", 1);

        // Act
        var response = await _client.DeleteAsync($"/api/services/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – soft-deleted
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Services.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == created.Id);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteService_WhenServiceDoesNotExist_ReturnsNotFound()
    {
        // Arrange

        // Act
        var response = await _client.DeleteAsync("/api/services/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- GetServicesLinkedToTaxId ---------------

    [Fact]
    public async Task GetServicesLinkedToTaxId_WhenTaxExists_ReturnsOk()
    {
        // Arrange – use Tax Id=2 (IsDeleted=false); link seeded Service Id=1 to it
        await _client.PutAsJsonAsync("/api/tax/2/link?itemsAreProducts=false", new[] { 1 });

        // Act
        var response = await _client.GetAsync("/api/services/tax/2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ServiceResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
    }

    // --------------- GetServicesLinkedToItemDiscountId ---------------

    [Fact]
    public async Task GetServicesLinkedToItemDiscountId_WhenDiscountExists_ReturnsOk()
    {
        // Arrange – create a fresh ItemDiscount with null dates (filter requires both null for no-timestamp query)
        var discountResp = await _client.PostAsJsonAsync("/api/item-discount",
            new ItemDiscountRequest { Value = 5, IsPercentage = true, Description = "Test", StartDate = null, EndDate = null });
        discountResp.EnsureSuccessStatusCode();
        var discount = (await discountResp.Content.ReadFromJsonAsync<ItemDiscountResponse>())!;
        await _client.PutAsJsonAsync($"/api/item-discount/{discount.Id}/link?itemsAreProducts=false", new[] { 1 });

        // Act
        var response = await _client.GetAsync($"/api/services/item-discount/{discount.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ServiceResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
    }

    // ---- helpers ----

    private async Task<ServiceResponse> CreateServiceAsync(
        string name, string description, int duration, int price, string imageUrl, int employeeId)
    {
        var url = string.IsNullOrEmpty(imageUrl) ? "https://example.com/service.jpg" : imageUrl;
        var response = await _client.PostAsJsonAsync("/api/services",
            new ServiceRequest
            {
                Name = name,
                Description = description,
                Duration = duration,
                Price = price,
                ImageURL = url,
                EmployeeId = employeeId
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ServiceResponse>())!;
    }
}
