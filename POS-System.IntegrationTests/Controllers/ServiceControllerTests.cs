using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

/// <summary>
/// Seeded services:
///   Id=1  ServiceId=1  "Service1"  IsDeleted=false  EmployeeId=1
///   Id=2  ServiceId=2  "Service2"  IsDeleted=true
///   Id=3  ServiceId=3  "Service3"  IsDeleted=true
///   Id=4  ServiceId=2  "Service2 v2"  IsDeleted=false  EmployeeId=3
/// </summary>
public class ServiceControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;
    private readonly HttpClient _writeClient;
    private readonly HttpClient _anonClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ServiceControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _readClient  = factory.CreateClientWithClaims("ServiceRead", "ItemRead");
        _writeClient = factory.CreateClientWithClaims("ServiceRead", "ServiceWrite", "ItemRead");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Helper to create a service through the API
    private async Task<ServiceResponse> CreateServiceAsync(string name = "Test Service")
    {
        var request = new ServiceRequest
        {
            Name        = name,
            Description = "Created by integration test",
            Duration    = 30,
            Price       = 1500,
            ImageURL    = "https://example.com/test.jpg",
            EmployeeId  = 1  // seeded employee version Id=1
        };
        var response = await _writeClient.PostAsJsonAsync("/api/services", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ServiceResponse>(body, JsonOptions)!;
    }

    // ── GET /api/services ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithServiceReadClaim_ReturnsOkWithPagedResult()
    {
        // Arrange — seeded services exist

        // Act
        var response = await _readClient.GetAsync("/api/services");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<ServiceResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/services");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/services/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithService()
    {
        // Arrange — service Id=1 is active

        // Act
        var response = await _readClient.GetAsync("/api/services/1");
        var body = await response.Content.ReadAsStringAsync();
        var service = JsonSerializer.Deserialize<ServiceResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        service.Should().NotBeNull();
        service!.Id.Should().Be(1);
        service.Name.Should().Be("Service1");
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _readClient.GetAsync("/api/services/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/services ────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsOkAndPersistsService()
    {
        // Arrange
        var request = new ServiceRequest
        {
            Name        = "Haircut Premium",
            Description = "Full styling service",
            Duration    = 60,
            Price       = 3500,
            ImageURL    = "https://example.com/haircut.jpg",
            EmployeeId  = 1
        };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/services", request);
        var body = await response.Content.ReadAsStringAsync();
        var service = JsonSerializer.Deserialize<ServiceResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        service.Should().NotBeNull();
        service!.Name.Should().Be("Haircut Premium");
        service.Price.Should().Be(3500);

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Services.FindAsync(service.Id);
        persisted.Should().NotBeNull();
        persisted!.Duration.Should().Be(60);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ServiceRequest
        {
            Name = "Unauthorized Service", Description = "Test",
            Duration = 30, Price = 100, ImageURL = "https://example.com/test.jpg", EmployeeId = 1
        };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/services", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/services/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithExistingId_ReturnsOkWithUpdatedService()
    {
        // Arrange — update service Id=1
        var request = new ServiceRequest
        {
            Name        = "Service1 Updated",
            Description = "Updated description",
            Duration    = 90,
            Price       = 2999,
            ImageURL    = "https://example.com/updated.jpg",
            EmployeeId  = 1
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/services/1", request);
        var body = await response.Content.ReadAsStringAsync();
        var service = JsonSerializer.Deserialize<ServiceResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        service!.Name.Should().Be("Service1 Updated");
        service.Duration.Should().Be(90);
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ServiceRequest
        {
            Name = "Ghost", Description = "Ghost", Duration = 1, Price = 1, ImageURL = "https://example.com/ghost.jpg", EmployeeId = 1
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/services/9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/services/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingId_ReturnsNoContentAndSoftDeletesService()
    {
        // Arrange — service Id=1 is active; create a fresh one to avoid affecting other tests
        var created = await CreateServiceAsync("Service To Delete");

        // Act
        var response = await _writeClient.DeleteAsync($"/api/services/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – soft-deleted in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Services.FindAsync(created.Id);
        persisted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _writeClient.DeleteAsync("/api/services/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
