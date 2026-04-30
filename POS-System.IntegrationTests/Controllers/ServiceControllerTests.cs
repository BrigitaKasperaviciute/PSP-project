using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ServiceControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ServiceControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ServiceOnTaxes.RemoveRange(db.ServiceOnTaxes.ToList());
        db.ServiceOnItemDiscounts.RemoveRange(db.ServiceOnItemDiscounts.ToList());
        db.ItemDiscounts.RemoveRange(db.ItemDiscounts.ToList());
        db.Services.RemoveRange(db.Services.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllServices_WithValidAuth_ReturnsOkWithServiceList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Services.Add(new Service { Id = 1001, ServiceId = 1001, Name = "Haircut", Description = "A basic haircut", Duration = 30, Price = 1500, ImageURL = "", EmployeeId = 1, Version = DateTime.UtcNow, IsDeleted = false });
        db.Services.Add(new Service { Id = 1002, ServiceId = 1002, Name = "Manicure", Description = "Nail care", Duration = 45, Price = 2000, ImageURL = "", EmployeeId = 1, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await client.GetAsync("/api/services");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>(_jsonOptions);
        body!.Results.Should().Contain(s => s.Name == "Haircut");
    }

    [Fact]
    public async Task GetAllServices_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/services");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceById_WithExistingId_ReturnsOkWithService()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Services.Add(new Service { Id = 1003, ServiceId = 1003, Name = "Pedicure", Description = "Foot care", Duration = 60, Price = 2500, ImageURL = "", EmployeeId = 1, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await client.GetAsync("/api/services/1003");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>(_jsonOptions);
        body!.Name.Should().Be("Pedicure");
        body.Duration.Should().Be(60);
        body.Price.Should().Be(2500);
    }

    [Fact]
    public async Task GetServiceById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await client.GetAsync("/api/services/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateService_WithValidRequest_ReturnsOkWithCreatedService()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceWrite");
        var request = new ServiceRequest
        {
            Name = "Massage",
            Description = "Full body massage",
            Duration = 90,
            Price = 5000,
            ImageURL = "https://test.com/massage.jpg",
            EmployeeId = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/services", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>(_jsonOptions);
        body!.Name.Should().Be("Massage");
        body.Duration.Should().Be(90);
        body.Price.Should().Be(5000);
        body.Id.Should().BeGreaterThan(0);

        // Verify database state
        using var assertScope = _factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Services.FindAsync(body.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Massage");
        saved.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateService_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new ServiceRequest { Name = "X", Description = "X", Duration = 10, Price = 100, ImageURL = "", EmployeeId = 1 };

        // Act
        var response = await client.PostAsJsonAsync("/api/services", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateService_WithExistingId_ReturnsOkWithUpdatedService()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Services.Add(new Service { Id = 1004, ServiceId = 1004, Name = "Old Service", Description = "Old desc", Duration = 20, Price = 1000, ImageURL = "", EmployeeId = 1, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceWrite");
        var request = new ServiceRequest { Name = "Updated Service", Description = "New desc", Duration = 40, Price = 3000, ImageURL = "https://test.com/updated.jpg", EmployeeId = 2 };

        // Act
        var response = await client.PutAsJsonAsync("/api/services/1004", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>(_jsonOptions);
        body!.Name.Should().Be("Updated Service");
        body.Duration.Should().Be(40);

        // Verify old record is now marked deleted
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var old = await assertDb.Services.FindAsync(1004);
        old!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateService_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceWrite");
        var request = new ServiceRequest { Name = "X", Description = "X", Duration = 10, Price = 100, ImageURL = "https://test.com/img.jpg", EmployeeId = 1 };

        // Act
        var response = await client.PutAsJsonAsync("/api/services/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteService_WithExistingId_ReturnsNoContentAndSoftDeletesService()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Services.Add(new Service { Id = 1005, ServiceId = 1005, Name = "To delete", Description = "desc", Duration = 15, Price = 500, ImageURL = "", EmployeeId = 1, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ServiceWrite");

        // Act
        var response = await client.DeleteAsync("/api/services/1005");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify soft delete in database
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await assertDb.Services.FindAsync(1005);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteService_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ServiceWrite");

        // Act
        var response = await client.DeleteAsync("/api/services/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET LINKED ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetServicesLinkedToTax_WithTaxId_ReturnsOkWithEmptyList()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/services/tax/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ServiceResponse>>(_jsonOptions);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetServicesLinkedToItemDiscount_WithExistingDiscountId_ReturnsOkWithEmptyList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ItemDiscounts.Add(new ItemDiscount { Id = 1006, ItemDiscountId = 1006, Value = 5, IsPercentage = true, Description = "Test", StartDate = null, EndDate = null, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/services/item-discount/1006");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ServiceResponse>>(_jsonOptions);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetServicesLinkedToItemDiscount_WithNonExistentDiscountId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("ItemRead");

        // Act
        var response = await client.GetAsync("/api/services/item-discount/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
