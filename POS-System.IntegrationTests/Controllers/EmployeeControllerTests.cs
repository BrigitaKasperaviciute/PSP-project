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

public class EmployeeControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;
    private readonly HttpClient _writeClient;
    private readonly HttpClient _anonClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EmployeeControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _readClient  = factory.CreateClientWithClaims("EmployeesRead");
        _writeClient = factory.CreateClientWithClaims("EmployeesRead", "EmployeesWrite");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET /api/employees ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithEmployeesReadClaim_ReturnsOkWithPagedResult()
    {
        // Arrange — 5 employees are seeded

        // Act
        var response = await _readClient.GetAsync("/api/employees");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<EmployeeResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/employees/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithEmployee()
    {
        // Arrange — employee Id=1 (johndoe) is seeded

        // Act
        var response = await _readClient.GetAsync("/api/employees/1");
        var body = await response.Content.ReadAsStringAsync();
        var employee = JsonSerializer.Deserialize<EmployeeResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        employee.Should().NotBeNull();
        employee!.Id.Should().Be(1);
        employee.UserName.Should().Be("johndoe");
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _readClient.GetAsync("/api/employees/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/employees/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task Update_WithExistingId_ReturnsOkWithUpdatedEmployee()
    {
        // Arrange — update employee Id=3 (adamsmith)
        var request = new EmployeeRequest(
            FirstName: "AdamUpdated",
            LastName: "SmithUpdated",
            BirthDate: new DateOnly(2003, 1, 29),
            UserName: "adamsmith",
            Email: "adamsmith@example.com",
            PhoneNumber: "4352335255",
            RoleId: 2 // Cashier
        );

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/employees/3", request);
        var body = await response.Content.ReadAsStringAsync();
        var employee = JsonSerializer.Deserialize<EmployeeResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        employee!.FirstName.Should().Be("AdamUpdated");
        employee.LastName.Should().Be("SmithUpdated");

        // Assert – database reflects the change
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Employees.FindAsync(3);
        persisted!.FirstName.Should().Be("AdamUpdated");
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new EmployeeRequest(
            "Ghost", "User", new DateOnly(1990, 1, 1),
            "ghost", "ghost@test.com", "12345678901", 2
        );

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/employees/9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/employees/{id} ───────────────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndSoftDeletesEmployee()
    {
        // Arrange — delete employee Id=4 (bobjohnson)

        // Act
        var response = await _writeClient.DeleteAsync("/api/employees/4");
        var body = await response.Content.ReadAsStringAsync();
        var employee = JsonSerializer.Deserialize<EmployeeResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        employee!.IsDeleted.Should().BeTrue();

        // Assert – soft-deleted in the database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Employees.FindAsync(4);
        persisted!.IsDeleted.Should().BeTrue();
        persisted.EndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _writeClient.DeleteAsync("/api/employees/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
