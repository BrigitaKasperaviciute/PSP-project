using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        _client = factory.CreateClientWithClaims("EmployeesRead", "EmployeesWrite");
    }

    // Employees are seeded – do not delete them; tests use seeded IDs.
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithValidAuth_ReturnsOkAndEmployees()
    {
        // Act
        var response = await _client.GetAsync("/api/employees?pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<EmployeeResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithSeededEmployeeId_ReturnsEmployee()
    {
        // Arrange – employee with ID=1 is seeded via migration
        const int seededEmployeeId = 1;

        // Act
        var response = await _client.GetAsync($"/api/employees/{seededEmployeeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(seededEmployeeId);
        body.UserName.Should().Be("johndoe");
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/employees/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_ReturnsOkAndVersionsEmployee()
    {
        // Arrange – update seeded employee 2 (janedoe)
        const int employeeId = 2;
        var updateRequest = new EmployeeRequest(
            FirstName: "Jane",
            LastName: "Updated",
            BirthDate: new DateOnly(1996, 11, 12),
            UserName: $"janeupdated-{Guid.NewGuid().ToString("N")[..6]}",
            Email: $"janeupdated-{Guid.NewGuid().ToString("N")[..8]}@example.com",
            PhoneNumber: "37060000001",
            RoleId: 1
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/employees/{employeeId}", updateRequest);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.LastName.Should().Be("Updated");
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        // Arrange
        var updateRequest = new EmployeeRequest(
            FirstName: "Ghost",
            LastName: "User",
            BirthDate: new DateOnly(2000, 1, 1),
            UserName: "ghostuser",
            Email: "ghost@example.com",
            PhoneNumber: "37060000000",
            RoleId: 1
        );

        // Act
        var response = await _client.PutAsJsonAsync("/api/employees/999999", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithValidEmployeeId_ReturnsOkAndSoftDeletesEmployee()
    {
        // Arrange – employee 3 (adamsmith) is available for soft-delete
        const int employeeId = 3;

        // Act
        var response = await _client.DeleteAsync($"/api/employees/{employeeId}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database: employee is soft-deleted (IsDeleted flag)
        await using var db = _factory.CreateDbContext();
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.Id == employeeId);
        employee.Should().NotBeNull();
        employee!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync("/api/employees/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_WithMissingWriteClaim_ForbidsUpdate()
    {
        // Arrange – client has only read claim, tries to update
        var readOnlyClient = _factory.CreateClientWithClaims("EmployeesRead");
        var updateRequest = new EmployeeRequest(
            FirstName: "Test",
            LastName: "Test",
            BirthDate: new DateOnly(2000, 1, 1),
            UserName: "testuser",
            Email: "test@example.com",
            PhoneNumber: "37060000000",
            RoleId: 1
        );

        // Act
        var response = await readOnlyClient.PutAsJsonAsync("/api/employees/1", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
