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

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class EmployeeControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public EmployeeControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    // Seeded employees have Ids 1–5; remove test-created employees only
    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.Employees.Where(e => e.Id > 5).ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetEmployeesAsync ---------------

    [Fact]
    public async Task GetEmployeesAsync_WhenEmployeesExist_ReturnsOkWithPagedResults()
    {
        // Arrange – seeded employees are present

        // Act
        var response = await _client.GetAsync("/api/employees?pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<EmployeeResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task GetEmployeesAsync_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/employees?pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- GetEmployeeByIdAsync ---------------

    [Fact]
    public async Task GetEmployeeByIdAsync_WhenEmployeeExists_ReturnsOkWithEmployee()
    {
        // Arrange – seeded Employee Id=1 (FirstName="John", LastName="Doe")
        const int existingId = 1;

        // Act
        var response = await _client.GetAsync($"/api/employees/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.FirstName.Should().Be("John");
        body.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task GetEmployeeByIdAsync_WhenEmployeeDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/employees/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- UpdateEmployeeByIdAsync ---------------

    [Fact]
    public async Task UpdateEmployeeByIdAsync_WithValidRequest_ReturnsOkAndUpdates()
    {
        // Arrange – update seeded Employee Id=2 (Jane Doe)
        const int employeeId = 2;
        var updateRequest = new EmployeeRequest(
            FirstName: "Jane",
            LastName: "Smith",
            BirthDate: new DateOnly(1996, 11, 12),
            UserName: "janesmith",
            Email: "janesmith@example.com",
            PhoneNumber: "77567455",
            RoleId: 1
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/employees/{employeeId}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.LastName.Should().Be("Smith");
        body.Email.Should().Be(updateRequest.Email);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == employeeId);
        persisted.Should().NotBeNull();
        persisted!.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task UpdateEmployeeByIdAsync_WhenEmployeeDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var request = new EmployeeRequest(
            FirstName: "Ghost",
            LastName: "User",
            BirthDate: new DateOnly(2000, 1, 1),
            UserName: "ghostuser",
            Email: "ghost@example.com",
            PhoneNumber: "00000000",
            RoleId: 0
        );

        // Act
        var response = await _client.PutAsJsonAsync("/api/employees/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- DeleteEmployeeByIdAsync ---------------

    [Fact]
    public async Task DeleteEmployeeByIdAsync_WhenEmployeeExists_ReturnsOkAndSoftDeletes()
    {
        // Arrange – use seeded Employee Id=3 (Adam Smith)
        const int employeeId = 3;

        // Act
        var response = await _client.DeleteAsync($"/api/employees/{employeeId}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – soft-deleted
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == employeeId);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteEmployeeByIdAsync_WhenEmployeeDoesNotExist_ReturnsNotFound()
    {
        // Arrange

        // Act
        var response = await _client.DeleteAsync("/api/employees/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
