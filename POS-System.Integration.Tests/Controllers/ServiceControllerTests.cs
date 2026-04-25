using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

public class ServiceControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public ServiceControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Seeded: Service Id=1 (Service1, IsDeleted=false) and Id=4 (Service2 v2, IsDeleted=false) are active.
    // Seeded employees: Id=1..5 available as EmployeeId.

    [Fact]
    public async Task GetAll_ValidToken_ReturnsOkWithPagedResult()
    {
        // Arrange
        // (seeded services)

        // Act
        var response = await _authClient.GetAsync("/api/services");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/services");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithService()
    {
        // Arrange
        const int existingId = 1; // seeded "Service1"

        // Act
        var response = await _authClient.GetAsync($"/api/services/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.Name.Should().Be("Service1");
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _authClient.GetAsync($"/api/services/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithCreatedService()
    {
        // Arrange
        var request = new ServiceRequest
        {
            Name = "IntegrationService",
            Description = "Test service desc",
            Duration = 30,
            Price = 1500,
            ImageURL = "http://example.com/img.png",
            EmployeeId = 1 // seeded employee
        };

        // Act
        var response = await _authClient.PostAsJsonAsync("/api/services", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("IntegrationService");
        body.Duration.Should().Be(30);
        body.Price.Should().Be(1500);
        body.IsDeleted.Should().BeFalse();

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Services.FirstOrDefaultAsync(s => s.Name == "IntegrationService");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ServiceRequest
        {
            Name = "NoAuth",
            Description = "x",
            Duration = 10,
            Price = 100,
            ImageURL = "http://example.com/img.png",
            EmployeeId = 1
        };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/services", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ExistingId_ReturnsOkWithUpdatedService()
    {
        // Arrange – create a service to update
        var created = await (await _authClient.PostAsJsonAsync("/api/services",
            new ServiceRequest { Name = "ServiceToUpdate", Description = "d", Duration = 15, Price = 100, ImageURL = "http://example.com/img.png", EmployeeId = 1 }))
            .Content.ReadFromJsonAsync<ServiceResponse>();
        var updateReq = new ServiceRequest
        {
            Name = "ServiceUpdated",
            Description = "updated",
            Duration = 60,
            Price = 200,
            ImageURL = "http://example.com/img.png",
            EmployeeId = 1
        };

        // Act
        var response = await _authClient.PutAsJsonAsync($"/api/services/{created!.Id}", updateReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("ServiceUpdated");
        body.Duration.Should().Be(60);
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ServiceRequest { Name = "x", Description = "x", Duration = 5, Price = 1, ImageURL = "http://example.com/img.png", EmployeeId = 1 };

        // Act
        var response = await _authClient.PutAsJsonAsync("/api/services/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsNoContentAndSoftDeletesService()
    {
        // Arrange – create a service to delete
        var created = await (await _authClient.PostAsJsonAsync("/api/services",
            new ServiceRequest { Name = "ServiceToDelete", Description = "d", Duration = 10, Price = 50, ImageURL = "http://example.com/img.png", EmployeeId = 1 }))
            .Content.ReadFromJsonAsync<ServiceResponse>();

        // Act
        var response = await _authClient.DeleteAsync($"/api/services/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deletedService = await db.Services.FindAsync(created.Id);
        deletedService!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        // (nothing to set up)

        // Act
        var response = await _authClient.DeleteAsync("/api/services/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetServicesLinkedToTaxId_ValidTaxId_ReturnsOkWithList()
    {
        // Arrange
        const int taxId = 2; // seeded tax

        // Act
        var response = await _authClient.GetAsync($"/api/services/tax/{taxId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ServiceResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetServicesLinkedToItemDiscountId_ValidId_ReturnsOkWithList()
    {
        // Arrange
        const int itemDiscountId = 2; // seeded active item discount

        // Act
        var response = await _authClient.GetAsync($"/api/services/item-discount/{itemDiscountId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ServiceResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetServicesLinkedToTaxId_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/services/tax/2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
