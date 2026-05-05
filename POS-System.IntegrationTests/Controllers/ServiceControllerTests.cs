using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;

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
        _client = factory.CreateClientWithClaims("ServiceRead", "ServiceWrite", "ItemRead");
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ServiceReservations.ExecuteDeleteAsync();
        await db.CartItems.Where(ci => ci.ServiceVersionId != null).ExecuteDeleteAsync();
        await db.ServiceOnTaxes.ExecuteDeleteAsync();
        await db.ServiceOnItemDiscounts.ExecuteDeleteAsync();
        await db.EmployeeOnServices.ExecuteDeleteAsync();
        await db.Services.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithValidAuth_ReturnsOkAndPagedServices()
    {
        // Arrange – employee ID 1 is always seeded
        await _client.PostAsJsonAsync("/api/services", new ServiceBuilder().WithEmployeeId(1).Build());

        // Act
        var response = await _client.GetAsync("/api/services?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<ServiceResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/services");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectService()
    {
        // Arrange
        var request = new ServiceBuilder().WithName("Haircut").WithPrice(3000).WithEmployeeId(1).Build();
        var created = await (await _client.PostAsJsonAsync("/api/services", request))
            .Content.ReadFromJsonAsync<ServiceResponse>();

        // Act
        var response = await _client.GetAsync($"/api/services/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Haircut");
        body.Price.Should().Be(3000);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/services/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsService()
    {
        // Arrange
        var request = new ServiceBuilder().WithName("Massage").WithPrice(5000).WithEmployeeId(1).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/services", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Massage");
        body.Price.Should().Be(5000);
        body.IsDeleted.Should().BeFalse();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Services.AsNoTracking().SingleOrDefaultAsync(s => s.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Massage");
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        // Arrange
        var readOnlyClient = _factory.CreateClientWithClaims("ServiceRead");

        // Act
        var response = await readOnlyClient.PostAsJsonAsync("/api/services", new ServiceBuilder().Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_CreatesNewVersionAndSoftDeletesOld()
    {
        // Arrange
        var original = new ServiceBuilder().WithName("OldService").WithPrice(1000).WithEmployeeId(1).Build();
        var created = await (await _client.PostAsJsonAsync("/api/services", original))
            .Content.ReadFromJsonAsync<ServiceResponse>();

        var updateRequest = new ServiceBuilder().WithName("NewService").WithPrice(2000).WithEmployeeId(1).Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/services/{created!.Id}", updateRequest);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        body!.Name.Should().Be("NewService");
        body.ServiceId.Should().Be(created.ServiceId);

        // Assert - old version soft-deleted
        await using var db = _factory.CreateDbContext();
        var oldVersion = await db.Services.AsNoTracking().SingleOrDefaultAsync(s => s.Id == created.Id);
        oldVersion!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.PutAsJsonAsync("/api/services/999999", new ServiceBuilder().Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsNoContentAndSoftDeletesService()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/services", new ServiceBuilder().WithEmployeeId(1).Build()))
            .Content.ReadFromJsonAsync<ServiceResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/services/{created!.Id}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database: service is soft-deleted
        await using var db = _factory.CreateDbContext();
        var inDb = await db.Services.AsNoTracking().SingleOrDefaultAsync(s => s.Id == created.Id);
        inDb!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync("/api/services/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
