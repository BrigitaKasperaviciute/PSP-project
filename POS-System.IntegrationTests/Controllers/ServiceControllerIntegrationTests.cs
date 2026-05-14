using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class ServiceControllerIntegrationTests
{
    [Fact]
    public async Task GetAll_WithReadClaim_ReturnsPagedServices()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceRead" });

        // Act
        var response = await client.GetAsync("/api/services?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThan(0);
        body.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_WithReadClaim_ReturnsService()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceRead" });

        // Act
        var response = await client.GetAsync("/api/services/4");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(4);
        body.Name.Should().Contain("Service2");
    }

    [Fact]
    public async Task CreateService_ValidPayloadPersistsServiceAndReturnsResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceRead", "ServiceWrite" });

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Services.CountAsync());
        var payload = new ServiceRequest
        {
            Name = $"Integration Service {Guid.NewGuid():N}"[..22],
            Description = "Integration test service",
            Duration = 60,
            Price = 1200,
            ImageURL = "https://example.com/service.png",
            EmployeeId = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/services", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdService = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        createdService.Should().NotBeNull();
        createdService!.Name.Should().Be(payload.Name);
        createdService.Description.Should().Be(payload.Description);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Services.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task UpdateService_ExistingServicePersistsChangesAndReturnsResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceWrite" });

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Services.CountAsync());
        var payload = new ServiceRequest
        {
            Name = "Updated Service",
            Description = "Updated description",
            Duration = 30,
            Price = 900,
            ImageURL = "https://example.com/updated.png",
            EmployeeId = 1
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/services/4", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedService = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        updatedService.Should().NotBeNull();
        updatedService!.Name.Should().Be(payload.Name);
        updatedService.Duration.Should().Be(payload.Duration);

        var persistedService = await factory.ExecuteDbContextAsync(db => db.Services.SingleAsync(x => x.Id == 4));
        persistedService.IsDeleted.Should().BeTrue();

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Services.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task DeleteService_ExistingServiceSoftDeletesServiceAndReturnsNoContent()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ServiceWrite" });

        // Arrange

        // Act
        var response = await client.DeleteAsync("/api/services/4");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deletedService = await factory.ExecuteDbContextAsync(db => db.Services.SingleAsync(x => x.Id == 4));
        deletedService.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetServicesLinkedToTaxId_WithReadClaim_ReturnsLinkedServices()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemRead" });

        // Act
        var response = await client.GetAsync("/api/services/tax/4");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ServiceResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetServicesLinkedToItemDiscountId_WithReadClaim_ReturnsLinkedServices()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemRead" });

        // Act
        var response = await client.GetAsync("/api/services/item-discount/2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ServiceResponse>>();
        body.Should().NotBeNull();
    }
}